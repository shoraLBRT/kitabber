#!/usr/bin/env pwsh
# Self-test of the verification itself (E01-02), run as a step of scripts/verify.ps1:
# - a failing step (a failed test or linter) makes verify exit non-zero and shows up in the run summary;
# - test reports (TRX, JUnit) reach the run summary;
# - the linters really reject bad code;
# - the CI workflow runs verify.ps1 within 10 minutes, with caches and test reports.
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
    Assert ($ci -match 'gitleaks_\$\{GITLEAKS_VERSION\}_checksums\.txt') 'ci.yml: gitleaks must be installed and its checksum checked for the secret scan'
}
finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}

if ($problems) {
    $problems | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host 'verify self-test: OK'
