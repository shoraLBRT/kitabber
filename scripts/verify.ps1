#!/usr/bin/env pwsh
# The single verification command: run before every PR; CI runs the same script.
# Each kind of check is added here by the task that introduces it.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$reports = Join-Path $root 'artifacts/test-results'
. (Join-Path $PSScriptRoot 'verify-lib.ps1')

Step 'required docs' {
    $required = 'CLAUDE.md', 'README.md', 'docs/spec.md', 'docs/ROADMAP.md', 'docs/PROJECT_STATE.md',
    'docs/TESTING.md', 'docs/ARCHITECTURE.md', 'docs/telegram-bridge.md', 'docs/unified-accounts.md', 'docs/content-policy.md'
    $missing = $required | Where-Object { -not (Test-Path $_) }
    if ($missing) { throw "missing: $($missing -join ', ')" }
}

Step 'markdown links' {
    $broken = @()
    $files = Get-ChildItem -Recurse -File -Filter *.md |
        Where-Object { $_.FullName -notmatch '[\\/](node_modules|bin|obj|\.git|\.nuxt|\.output)[\\/]' -and $_.Name -ne 'CHANGELOG.md' }
    foreach ($file in $files) {
        $text = Get-Content -Raw -Encoding utf8 -LiteralPath $file.FullName
        foreach ($m in [regex]::Matches($text, '\]\(([^)\s]+)\)')) {
            $target = $m.Groups[1].Value
            if ($target -match '^(https?:|mailto:|#)') { continue }
            $path = ($target -split '#')[0]
            if (-not $path) { continue }
            if (-not (Test-Path (Join-Path $file.DirectoryName $path))) {
                $broken += "$($file.FullName.Substring($root.Length + 1)): $target"
            }
        }
    }
    if ($broken) { throw "broken links:`n$($broken -join "`n")" }
}

Step 'ui specs' { & (Join-Path $PSScriptRoot 'ui-specs-test.ps1') }

Step 'json files' {
    $files = Get-ChildItem -File -Filter *.json -Force
    $files += Get-ChildItem -File -Filter .*.json -Force
    foreach ($file in ($files | Sort-Object FullName -Unique)) {
        try { Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json | Out-Null }
        catch { throw "$($file.Name): $_" }
    }
}

Step 'repository layout' {
    # The skeleton from E01-01: every top-level part exists and docs/ARCHITECTURE.md describes it.
    $parts = [ordered]@{
        'backend'  = 'backend/Platform.slnx', 'backend/Directory.Build.props', 'backend/Directory.Packages.props'
        'frontend' = 'frontend/package.json', 'frontend/pnpm-lock.yaml', 'frontend/nuxt.config.ts'
        'admin'    = 'admin/package.json', 'admin/pnpm-lock.yaml', 'admin/vite.config.ts'
    }
    $files = @($parts.Values | ForEach-Object { $_ }) + 'global.json', '.editorconfig', '.prettierrc.json'
    $missing = $files | Where-Object { -not (Test-Path $_) }
    if ($missing) { throw "missing: $($missing -join ', ')" }
    $architecture = Get-Content -Raw -Encoding utf8 'docs/ARCHITECTURE.md'
    $undocumented = $parts.Keys | Where-Object { -not $architecture.Contains("``$_/``") }
    if ($undocumented) { throw "docs/ARCHITECTURE.md does not describe: $($undocumented -join ', ')" }
}

Step 'dev environment config' { & (Join-Path $PSScriptRoot 'dev-env-test.ps1') }

Step 'dev environment smoke' {
    # Starts compose.yaml for real. Locally it is skipped when Docker is not running (or VERIFY_SKIP_DOCKER=1);
    # CI must always run it.
    $why = $null
    if ($env:VERIFY_SKIP_DOCKER -eq '1') { $why = 'VERIFY_SKIP_DOCKER=1' }
    elseif (-not (Get-Command docker -ErrorAction SilentlyContinue)) { $why = 'docker is not installed' }
    else {
        & { $ErrorActionPreference = 'Continue'; docker info 2>&1 | Out-Null }
        if ($LASTEXITCODE) { $why = 'the Docker daemon is not running' }
        $global:LASTEXITCODE = 0
    }
    if ($why -and $env:CI) { throw "CI must run the dev environment smoke test: $why" }
    if ($why) { Skip-Step $why }
    & (Join-Path $PSScriptRoot 'dev-env-test.ps1') -Smoke
}

Step 'deploy config' { & (Join-Path $PSScriptRoot 'deploy-test.ps1') }

Step 'deploy smoke' {
    # Builds the images, then deploys, upgrades and rolls back the stage compose file on this Docker (E01-03). It takes
    # minutes, so CI runs it in its own job (images / build, checked by the deploy config step and the self-test),
    # not inside the 10 minutes of verify; locally it runs with VERIFY_IMAGES=1.
    if ($env:VERIFY_IMAGES -ne '1') { Skip-Step 'runs in CI job images / build; set VERIFY_IMAGES=1 to run it here' }
    & (Join-Path $PSScriptRoot 'deploy-test.ps1') -Smoke
}

Step 'secret scan' {
    # gitleaks over every file git would commit (scripts/secret-scan.ps1, docs/CONFIG.md). Locally it is skipped
    # when gitleaks is not installed; CI installs it and must always run it.
    if (-not (Get-Command gitleaks -ErrorAction SilentlyContinue)) {
        if ($env:CI) { throw 'CI must run the secret scan: gitleaks is not installed' }
        Skip-Step 'gitleaks is not installed (https://github.com/gitleaks/gitleaks#installing)'
    }
    & (Join-Path $PSScriptRoot 'secret-scan.ps1')
}

$solution = Get-ChildItem -Path backend -Filter *.sln* -ErrorAction SilentlyContinue | Select-Object -First 1
if ($solution) {
    Step 'backend build' { dotnet build $solution.FullName --nologo -v q -warnaserror }
    Step 'backend format' { dotnet format $solution.FullName --verify-no-changes -v q }
    Step 'backend tests' {
        dotnet test $solution.FullName --nologo --no-build -v q --logger trx --results-directory (Join-Path $reports 'backend')
    }
}

foreach ($app in 'frontend', 'admin') {
    if (Test-Path "$app/package.json") {
        Push-Location $app
        try {
            Step "$app install" { pnpm install --frozen-lockfile --silent }
            Step "$app lint" { pnpm run --if-present lint }
            Step "$app typecheck" { pnpm run --if-present typecheck }
            Step "$app build" { pnpm run --silent build }
            Step "$app unit tests" { pnpm run --if-present test }
            Step "$app e2e (desktop + mobile)" {
                # Playwright against the build above in the profiles desktop and mobile (E01-16, docs/TESTING.md). Only
                # Chromium is installed (headless shell, reused when present); on the Linux CI runner with its libraries.
                $install = 'exec', 'playwright', 'install', 'chromium', '--only-shell'
                if ($env:CI -and $IsLinux) { $install += '--with-deps' }
                pnpm @install
                if ($LASTEXITCODE) { throw "playwright install: exit code $LASTEXITCODE" }
                pnpm run e2e
            }
        }
        finally { Pop-Location }
    }
}

Step 'verify self-test' { & (Join-Path $PSScriptRoot 'verify-selftest.ps1') }

exit (Complete-Verify $reports)
