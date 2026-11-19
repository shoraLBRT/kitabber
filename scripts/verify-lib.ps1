# The harness behind scripts/verify.ps1: named steps, a result table, test reports and the exit code.
# Dot-source it (". ./scripts/verify-lib.ps1"); scripts/verify-selftest.ps1 checks it.
$script:VerifyResults = [System.Collections.Generic.List[object]]::new()

function Step($name, [scriptblock]$body) {
    Write-Host "== $name"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $status = 'ok'
    try {
        & $body
        if ($LASTEXITCODE) { throw "exit code $LASTEXITCODE" }
    }
    catch [System.OperationCanceledException] {
        Write-Host "SKIPPED: $name - $($_.Exception.Message)"
        $status = 'skipped'
    }
    catch {
        Write-Host "FAILED: $name - $_"
        $status = 'failed'
    }
    $global:LASTEXITCODE = 0
    $script:VerifyResults.Add([pscustomobject]@{ Name = $name; Status = $status; Seconds = [math]::Round($watch.Elapsed.TotalSeconds, 1) })
}

# Ends the current step as skipped: shown in the summary, does not fail verify. Only for a check that cannot run
# on this machine; a check that CI must run refuses to skip there itself.
function Skip-Step([string]$Reason) {
    throw [System.OperationCanceledException]::new($Reason)
}

# Test reports: TRX (dotnet test --logger trx) and JUnit XML (Vitest, Playwright) under $Directory.
function Get-TestReports([string]$Directory) {
    if (-not (Test-Path $Directory)) { return @() }
    Get-ChildItem -Path $Directory -Recurse -File -Include *.trx, *.xml | Sort-Object FullName | ForEach-Object {
        [xml]$xml = Get-Content -Raw -Encoding utf8 -LiteralPath $_.FullName
        $relative = $_.FullName.Substring((Resolve-Path $Directory).Path.TrimEnd('\', '/').Length + 1) -replace '\\', '/'
        if ($xml.TestRun) {
            $c = $xml.TestRun.ResultSummary.Counters
            [pscustomobject]@{ Report = $relative; Total = [int]$c.total; Passed = [int]$c.passed; Failed = [int]$c.failed + [int]$c.error; Skipped = [int]$c.notExecuted }
        }
        elseif ($xml.testsuites -or $xml.testsuite) {
            $suites = if ($xml.testsuites) { @($xml.testsuites.testsuite) } else { @($xml.testsuite) }
            $total = ($suites | ForEach-Object { [int]$_.tests } | Measure-Object -Sum).Sum
            $failed = ($suites | ForEach-Object { [int]$_.failures + [int]$_.errors } | Measure-Object -Sum).Sum
            $skipped = ($suites | ForEach-Object { [int]$_.skipped } | Measure-Object -Sum).Sum
            [pscustomobject]@{ Report = $relative; Total = $total; Passed = $total - $failed - $skipped; Failed = $failed; Skipped = $skipped }
        }
    }
}

function Format-VerifySummary([string]$ReportsDirectory) {
    $failed = @($script:VerifyResults | Where-Object Status -eq 'failed')
    $lines = @("## verify: $(if ($failed) { 'FAILED' } else { 'OK' })", '', '| Step | Result | Seconds |', '| --- | --- | --- |')
    $lines += $script:VerifyResults | ForEach-Object { "| $($_.Name) | $(if ($_.Status -eq 'failed') { '**failed**' } else { $_.Status }) | $($_.Seconds) |" }
    $lines += '', '### Tests', ''
    $reports = @(Get-TestReports $ReportsDirectory)
    if ($reports) {
        $lines += '| Report | Total | Passed | Failed | Skipped |', '| --- | --- | --- | --- | --- |'
        $lines += $reports | ForEach-Object { "| $($_.Report) | $($_.Total) | $($_.Passed) | $($_.Failed) | $($_.Skipped) |" }
    }
    else { $lines += 'No test reports yet.' }
    $lines -join "`n"
}

# Prints the outcome, appends the summary to the GitHub run summary when there is one, returns the exit code.
function Complete-Verify([string]$ReportsDirectory) {
    if ($env:GITHUB_STEP_SUMMARY) {
        Add-Content -Path $env:GITHUB_STEP_SUMMARY -Encoding utf8 -Value (Format-VerifySummary $ReportsDirectory)
    }
    $skipped = @($script:VerifyResults | Where-Object Status -eq 'skipped' | ForEach-Object Name)
    if ($skipped) { Write-Host "`nverify: skipped ($($skipped -join '; '))" }
    $failed = @($script:VerifyResults | Where-Object Status -eq 'failed' | ForEach-Object Name)
    if ($failed) {
        Write-Host "`nverify: FAILED ($($failed -join '; '))"
        return 1
    }
    Write-Host "`nverify: OK"
    return 0
}
