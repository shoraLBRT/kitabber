#!/usr/bin/env pwsh
# The single verification command: run before every PR; CI runs the same script.
# Each kind of check is added here by the task that introduces it.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$failed = @()

function Step($name, [scriptblock]$body) {
    Write-Host "== $name"
    try {
        & $body
        if ($LASTEXITCODE) { throw "exit code $LASTEXITCODE" }
    }
    catch {
        Write-Host "FAILED: $name - $_"
        $script:failed += $name
    }
    $global:LASTEXITCODE = 0
}

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
        $text = Get-Content -Raw -Encoding utf8 $file.FullName
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

Step 'json files' {
    $files = Get-ChildItem -File -Filter *.json -Force
    $files += Get-ChildItem -File -Filter .*.json -Force
    foreach ($file in ($files | Sort-Object FullName -Unique)) {
        try { Get-Content -Raw $file.FullName | ConvertFrom-Json | Out-Null }
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

$solution = Get-ChildItem -Path backend -Filter *.sln* -ErrorAction SilentlyContinue | Select-Object -First 1
if ($solution) {
    Step 'backend build' { dotnet build $solution.FullName --nologo -v q -warnaserror }
    Step 'backend format' { dotnet format $solution.FullName --verify-no-changes -v q }
    Step 'backend tests' { dotnet test $solution.FullName --nologo --no-build -v q }
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
            Step "$app e2e (desktop + mobile)" { pnpm run --if-present e2e }
        }
        finally { Pop-Location }
    }
}

if ($failed) {
    Write-Host "`nverify: FAILED ($($failed -join '; '))"
    exit 1
}
Write-Host "`nverify: OK"
