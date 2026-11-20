#!/usr/bin/env pwsh
# Self-test of the verification itself (E01-02), run as a step of scripts/verify.ps1:
# - a failing step (a failed test or linter) makes verify exit non-zero and shows up in the run summary;
# - test reports (TRX, JUnit) reach the run summary;
# - the linters really reject bad code;
# - the CI workflow runs verify.ps1 within 10 minutes, with caches and test reports;
# - the screen-spec check (scripts/ui-specs-test.ps1) rejects broken specs.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$shell = (Get-Process -Id $PID).Path
$temp = Join-Path ([System.IO.Path]::GetTempPath()) "verify-selftest-$PID"
$problems = @()

function Assert($condition, [string]$message) {
    if (-not $condition) { $script:problems += $message }
}

New-Item -ItemType Directory -Force $temp | Out-Null
try {
    # 1. The harness: failed steps fail the run and appear in the summary next to the test reports.
    $reports = Join-Path $temp 'reports'
    New-Item -ItemType Directory -Force (Join-Path $reports 'backend'), (Join-Path $reports 'frontend') | Out-Null
    Set-Content -Encoding utf8 (Join-Path $reports 'backend/sample.trx') @'
<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary outcome="Failed"><Counters total="3" executed="3" passed="2" failed="1" error="0" notExecuted="0" /></ResultSummary>
</TestRun>
'@
    # dotnet test names a colliding report run[1].trx: the brackets must not be read as a wildcard.
    Copy-Item -LiteralPath (Join-Path $reports 'backend/sample.trx') -Destination (Join-Path $reports 'backend/sample[1].trx')
    Set-Content -Encoding utf8 (Join-Path $reports 'frontend/junit.xml') @'
<?xml version="1.0" encoding="UTF-8"?>
<testsuites><testsuite name="a" tests="4" failures="0" errors="0" skipped="1" /></testsuites>
'@
    $lib = Join-Path $PSScriptRoot 'verify-lib.ps1'
    $run = Join-Path $temp 'run.ps1'
    Set-Content -Encoding utf8 $run @"
param([switch]`$Fail)
. '$lib'
Step 'passing step' { }
Step 'skipped step' { Skip-Step 'no tool' }
if (`$Fail) {
    Step 'throwing step' { throw 'boom' }
    Step 'exit code step' { & '$shell' -NoProfile -Command 'exit 3' }
}
exit (Complete-Verify '$reports')
"@
    $outerSummary = $env:GITHUB_STEP_SUMMARY
    foreach ($fail in $true, $false) {
        $summary = Join-Path $temp "summary-$fail.md"
        $env:GITHUB_STEP_SUMMARY = $summary
        $runArgs = @('-NoProfile', '-File', $run) + @(if ($fail) { '-Fail' })
        try { & $shell @runArgs | Out-Null; $code = $LASTEXITCODE }
        finally { $env:GITHUB_STEP_SUMMARY = $outerSummary }
        $text = if (Test-Path $summary) { Get-Content -Raw -Encoding utf8 $summary } else { '' }
        if ($fail) {
            Assert ($code -eq 1) "harness: a failing step must exit 1, got $code"
            Assert ($text.Contains('## verify: FAILED')) 'harness: summary must say FAILED'
            Assert ($text.Contains('| throwing step | **failed** |')) 'harness: summary must mark a throwing step'
            Assert ($text.Contains('| exit code step | **failed** |')) 'harness: summary must mark a non-zero exit code'
        }
        else {
            Assert ($code -eq 0) "harness: passing steps must exit 0, got $code"
            Assert ($text.Contains('## verify: OK')) 'harness: summary must say OK'
            Assert ($text.Contains('| skipped step | skipped |')) 'harness: a skipped step must show as skipped and not fail the run'
        }
        Assert ($text.Contains('| backend/sample.trx | 3 | 2 | 1 | 0 |')) 'harness: TRX report must reach the summary'
        Assert ($text.Contains('| backend/sample[1].trx | 3 | 2 | 1 | 0 |')) 'harness: a report named with brackets must reach the summary'
        Assert ($text.Contains('| frontend/junit.xml | 4 | 3 | 0 | 1 |')) 'harness: JUnit report must reach the summary'
    }
    $global:LASTEXITCODE = 0

    # 2. The linters reject bad code (skipped for an app whose dependencies are not installed).
    # Native tools write to stderr; Windows PowerShell would turn that into a terminating error under Stop.
    $ErrorActionPreference = 'Continue'
    foreach ($app in 'frontend', 'admin') {
        $dir = Join-Path $root $app
        if (-not (Test-Path (Join-Path $dir 'node_modules'))) { continue }
        $bad = Join-Path $dir "verify-selftest-$PID.ts"
        Set-Content -Encoding utf8 $bad "export const  a: any=1`n"
        Push-Location $dir
        try {
            $out = pnpm exec eslint $bad 2>&1 | Out-String
            Assert ($LASTEXITCODE -ne 0 -and $out.Contains('no-explicit-any')) "${app}: ESLint must reject an explicit any"
            $out = pnpm exec prettier --check $bad 2>&1 | Out-String
            Assert ($LASTEXITCODE -ne 0) "${app}: Prettier must reject unformatted code"
        }
        finally { Pop-Location; Remove-Item -Force $bad; $global:LASTEXITCODE = 0 }
    }
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $cs = Join-Path $temp 'cs'
        New-Item -ItemType Directory -Force $cs | Out-Null
        Copy-Item (Join-Path $root '.editorconfig') $cs
        Set-Content -Encoding utf8 (Join-Path $cs 'Bad.cs') "namespace Bad;`npublic   class Bad{  }`n"
        dotnet format whitespace $cs --folder --verify-no-changes -v q 2>&1 | Out-Null
        Assert ($LASTEXITCODE -ne 0) 'backend: dotnet format must reject unformatted code'
        $global:LASTEXITCODE = 0
    }

    # 3. The CI workflow calls this script and keeps within the limits of E01-02.
    $ci = Get-Content -Raw -Encoding utf8 (Join-Path $root '.github/workflows/ci.yml')
    Assert ($ci -match '(?m)^\s+run: \./scripts/verify\.ps1\s*$') 'ci.yml: job verify must run ./scripts/verify.ps1'
    $timeout = [regex]::Match($ci, 'timeout-minutes:\s*(\d+)')
    Assert ($timeout.Success -and [int]$timeout.Groups[1].Value -le 10) 'ci.yml: verify must time out within 10 minutes'
    Assert ($ci -match 'cache:\s*pnpm') 'ci.yml: pnpm store must be cached'
    Assert ($ci -match '\.nuget/packages') 'ci.yml: NuGet packages must be cached'
    Assert ($ci -match 'artifacts/test-results') 'ci.yml: test reports must be uploaded'
    Assert ($ci -match 'path: ~/\.cache/ms-playwright') 'ci.yml: Playwright browsers must be cached'
    # #302: Chromium's system packages come from cached .deb files, not from the Ubuntu mirror on every run.
    Assert (($ci -match 'scripts/ci/playwright-deps\.sh plan') -and ($ci -match 'scripts/ci/playwright-deps\.sh install') -and
        ($ci -match 'path: ~/\.cache/playwright-deps')) 'ci.yml: Playwright system packages must be installed from a cache'
    $verifyScript = Get-Content -Raw -Encoding utf8 (Join-Path $root 'scripts/verify.ps1')
    Assert ($verifyScript -notmatch '--with-deps') 'verify.ps1: --with-deps would hit the Ubuntu mirror on every CI run'

    # 3a. verify.ps1 runs the client tests (E01-16): unit tests, then Playwright in both profiles after the build.
    $verify = Get-Content -Raw -Encoding utf8 (Join-Path $PSScriptRoot 'verify.ps1')
    foreach ($expected in 'pnpm run --if-present test', 'pnpm run e2e', 'pnpm exec playwright install chromium') {
        Assert $verify.Contains($expected) "verify.ps1: must run '$expected'"
    }
    Assert ($verify.IndexOf('"$app build"') -lt $verify.IndexOf('e2e (desktop + mobile)')) 'verify.ps1: e2e must run after the build'
    foreach ($app in 'frontend', 'admin') {
        $scripts = (Get-Content -Raw -Encoding utf8 (Join-Path $root "$app/package.json") | ConvertFrom-Json).scripts
        Assert ($scripts.e2e -eq 'playwright test') "$app/package.json: script e2e must run Playwright"
        Assert ($scripts.test -match "junit=\.\./artifacts/test-results/$app/") "$app/package.json: Vitest must write JUnit to artifacts/test-results/$app"
        Assert (Test-Path (Join-Path $root "$app/playwright.config.ts")) "$app/playwright.config.ts is missing"
    }
    Assert ($ci -match 'gitleaks_\$\{GITLEAKS_VERSION\}_checksums\.txt') 'ci.yml: gitleaks must be installed and its checksum checked for the secret scan'

    # 3b. Renovate sees the gitleaks version in ci.yml (#285): a regex manager of renovate.json covers ci.yml and
    # extracts exactly the version the install step uses, from GitHub releases.
    $renovate = Get-Content -Raw -Encoding utf8 (Join-Path $root 'renovate.json') | ConvertFrom-Json
    $installed = [regex]::Match($ci, '(?m)^\s+GITLEAKS_VERSION:\s*(\S+)\s*$').Groups[1].Value
    $found = $false
    foreach ($manager in @($renovate.customManagers) | Where-Object { $_ -and $_.customType -eq 'regex' }) {
        $files = @($manager.managerFilePatterns) | Where-Object { '.github/workflows/ci.yml' -match $_.Trim('/') }
        if (-not $files) { continue }
        foreach ($pattern in $manager.matchStrings) {
            $match = [regex]::Match($ci, $pattern)
            if ($match.Success -and $match.Groups['depName'].Value -eq 'gitleaks/gitleaks' -and
                $match.Groups['datasource'].Value -eq 'github-releases' -and $match.Groups['currentValue'].Value -eq $installed) {
                $found = $true
            }
        }
    }
    Assert ($installed -and $found) "renovate.json: a regex manager must extract the gitleaks version '$installed' from ci.yml"

    # 4. The screen-spec check (E02-03) rejects a broken tree: missing screen, sections, FR, tokens, anchors, links.
    # The state headings are Russian; the script stays ASCII so Windows PowerShell 5.1 reads it without a BOM.
    $empty, $loading, $failure = '\u041f\u0443\u0441\u0442\u043e', '\u0417\u0430\u0433\u0440\u0443\u0437\u043a\u0430', '\u041e\u0448\u0438\u0431\u043a\u0430' | ForEach-Object { [regex]::Unescape($_) }
    $tree = Join-Path $temp 'ui-tree'
    New-Item -ItemType Directory -Force (Join-Path $tree 'docs/ui'), (Join-Path $tree 'brand') | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'docs/spec.md') -Destination (Join-Path $tree 'docs')
    Copy-Item -LiteralPath (Join-Path $root 'brand/tokens.json'), (Join-Path $root 'brand/brand.json') -Destination (Join-Path $tree 'brand')
    Get-ChildItem -LiteralPath (Join-Path $root 'docs/ui') -Filter *.md | Where-Object Name -ne 'settings.md' |
        Copy-Item -Destination (Join-Path $tree 'docs/ui')
    Set-Content -Encoding utf8 -LiteralPath (Join-Path $tree 'docs/ARCHITECTURE.md') '# Architecture'
    $feed = Join-Path $tree 'docs/ui/feed.md'
    $text = (Get-Content -Raw -Encoding utf8 -LiteralPath $feed) -replace '390 px', '390' -replace "### $loading", '### x'
    $text += "`nFR-NOPE, ``--color-nope``, breakpoint.xl, [anchor](../spec.md#no-such-section)`n"
    Set-Content -Encoding utf8 -LiteralPath $feed $text
    Set-Content -Encoding utf8 -LiteralPath (Join-Path $tree 'docs/ui/post.md') "# Post`n`n## 1440 px`n## 390 px`n### $empty`n### $loading`n### $failure`n"
    $out = & $shell -NoProfile -File (Join-Path $PSScriptRoot 'ui-specs-test.ps1') -Root $tree 2>&1 | Out-String
    Assert ($LASTEXITCODE -ne 0) 'ui specs: a broken tree must fail'
    foreach ($expected in 'docs/ui/settings.md is missing', 'docs/ui/feed.md: no section for 390 px', "docs/ui/feed.md: no section for $loading",
        'docs/ui/feed.md: FR-NOPE is not a section', 'docs/ui/feed.md: unknown token --color-nope', 'unknown token breakpoint.xl',
        'no heading for ../spec.md#no-such-section', 'docs/ui/post.md: no FR requirement referenced', 'docs/ARCHITECTURE.md does not link ui/README.md') {
        Assert $out.Contains($expected) "ui specs: must report '$expected'"
    }
    $global:LASTEXITCODE = 0

    # 5. The images are built and the deploy is smoke-tested on every PR in their own job (E01-03), outside verify.
    $images = Get-Content -Raw -Encoding utf8 (Join-Path $root '.github/workflows/images.yml')
    Assert ($images -match '(?m)^  pull_request:') 'images.yml: must run on every pull request'
    Assert ($images -match '(?m)^  build:\s*$' -and $images -match '(?m)run: \./scripts/deploy-test\.ps1 -Smoke ') 'images.yml: job build must run scripts/deploy-test.ps1 -Smoke'
}
finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}

if ($problems) {
    $problems | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host 'verify self-test: OK'
