#!/usr/bin/env pwsh
# Builds (and with -Push publishes) the container images of every deployable part (E01-03, docs/DEPLOY.md).
#   pwsh scripts/images.ps1                                   build <registry>/<image>:local for each image
#   pwsh scripts/images.ps1 -Registry ghcr.io/o/r -Tags sha-1a2b3c4, latest -Push
# The list of images lives only here; scripts/deploy-test.ps1 checks that the stage compose file and the
# workflows use the same names.
param(
    [string]$Registry = 'local/kitabber',
    [string[]]$Tags = @('local'),
    [switch]$Push,
    [switch]$ListOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# Name => Dockerfile, build target (or $null), context; paths relative to the repository root.
$images = [ordered]@{
    'api'      = 'backend/Dockerfile', 'api', '.'
    'workers'  = 'backend/Dockerfile', 'workers', '.'
    'migrator' = 'backend/Dockerfile', 'migrator', '.'
    'frontend' = 'frontend/Dockerfile', $null, 'frontend'
    'admin'    = 'admin/Dockerfile', $null, 'admin'
}
if ($ListOnly) { return $images }

$Registry = $Registry.ToLowerInvariant().TrimEnd('/')
$labels = @()
if ($env:GITHUB_REPOSITORY) {
    $labels += '--label', "org.opencontainers.image.source=$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY"
}
if ($env:GITHUB_SHA) { $labels += '--label', "org.opencontainers.image.revision=$env:GITHUB_SHA" }

foreach ($name in $images.Keys) {
    $file, $target, $context = $images[$name]
    $refs = $Tags | ForEach-Object { "$Registry/${name}:$_" }
    Write-Host "== image $name ($($refs -join ', '))"
    $buildArgs = @('build', '--quiet', '-f', (Join-Path $root $file)) + $labels
    if ($target) { $buildArgs += '--target', $target }
    foreach ($ref in $refs) { $buildArgs += '-t', $ref }
    $buildArgs += (Join-Path $root $context)
    docker @buildArgs | Out-Null
    if ($LASTEXITCODE) { throw "docker build of $name failed (exit code $LASTEXITCODE)" }
    if ($Push) {
        foreach ($ref in $refs) {
            docker push --quiet $ref | Out-Null
            if ($LASTEXITCODE) { throw "docker push $ref failed (exit code $LASTEXITCODE)" }
        }
    }
}
