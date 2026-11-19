#!/usr/bin/env pwsh
# Deploys one image tag to a stand (E01-03, docs/DEPLOY.md). A rollback is the same command with an earlier tag.
#   pwsh scripts/deploy.ps1 -EnvFile stage.env -Tag sha-1a2b3c4
# Docker is reached the usual way: locally, or on the stand's host with DOCKER_HOST=ssh://user@host.
# Order: validate the compose file, pull the images, run the migrator of the new version (a failed migration stops
# the deploy and the running version stays), then replace the containers and wait for every healthcheck.
param(
    [Parameter(Mandatory)][string]$EnvFile,
    [Parameter(Mandatory)][string]$Tag,
    [string]$ComposeFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'deploy/compose.stage.yaml'),
    # The images are already on the Docker host (the smoke test builds them there).
    [switch]$NoPull
)
$ErrorActionPreference = 'Stop'
if ($Tag -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$') { throw "not a valid image tag: '$Tag'" }

function Invoke-Compose {
    & docker compose -f $ComposeFile --env-file $EnvFile @args
    if ($LASTEXITCODE) { throw "docker compose $($args -join ' ') failed (exit code $LASTEXITCODE)" }
}

# The tag on the command line wins over IMAGE_TAG in the env file.
$previousTag = $env:IMAGE_TAG
$env:IMAGE_TAG = $Tag
try {
    Invoke-Compose config --quiet
    $running = & docker compose -f $ComposeFile --env-file $EnvFile ps --format '{{.Image}}' api 2>$null | Select-Object -First 1
    $global:LASTEXITCODE = 0
    Write-Host "deploy: $Tag (running now: $(if ($running) { $running } else { 'nothing' }))"
    if (-not $NoPull) { Invoke-Compose --profile tools pull --quiet }
    Invoke-Compose run --rm --quiet-pull migrator migrate
    Invoke-Compose up --detach --wait --remove-orphans
    Write-Host "deploy: $Tag is up; roll back with -Tag <previous tag>"
}
finally {
    $env:IMAGE_TAG = $previousTag
}
