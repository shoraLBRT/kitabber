#!/usr/bin/env pwsh
# Secret scan (E01-13, ADR-0007), run as a step of scripts/verify.ps1: gitleaks with .gitleaks.toml over every file
# git would commit (tracked and untracked, not ignored), so uncommitted work is scanned too and git worktrees work.
# History needs no scan of its own: main gets only squash commits of PRs, and every PR tree passes this step in CI.
# A canary runs first: a planted token must be found, so a broken scanner or an over-wide allowlist fails the step.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$config = Join-Path $root '.gitleaks.toml'
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('secret-scan-' + [guid]::NewGuid().ToString('N'))

function Invoke-Gitleaks([string]$Path, [string[]]$Extra) {
    & { $ErrorActionPreference = 'Continue'; gitleaks dir $Path --config $config --no-banner --redact --exit-code 1 @Extra 2>&1 }
}

try {
    # 1. Canary: a GitHub token made up at run time (the pattern is split so this file holds none).
    $canary = Join-Path $work 'canary'
    New-Item -ItemType Directory -Force $canary | Out-Null
    $chars = [char[]]'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    $token = 'gh' + 'p_' + -join (1..36 | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
    Set-Content -LiteralPath (Join-Path $canary 'settings.txt') -Value "github_token = $token" -Encoding utf8
    Invoke-Gitleaks $canary @('--log-level', 'error') | Out-Null
    if ($LASTEXITCODE -ne 1) { throw "gitleaks did not find the planted token (exit code $LASTEXITCODE)" }
    $global:LASTEXITCODE = 0

    # 2. The files git would commit, copied with their paths.
    $tree = Join-Path $work 'tree'
    $files = & git -C $root -c core.quotepath=off ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE) { throw 'git ls-files failed' }
    foreach ($file in $files) {
        $source = Join-Path $root $file
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        $target = Join-Path $tree $file
        New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }
    $report = Invoke-Gitleaks $tree @('--verbose')
    if ($LASTEXITCODE) {
        $report | ForEach-Object { Write-Host $_ }
        throw "gitleaks found secrets in the repository (exit code $LASTEXITCODE); move them to environment variables or secret files (docs/CONFIG.md)"
    }
    Write-Host "secret scan: $(@($files).Count) files clean"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
