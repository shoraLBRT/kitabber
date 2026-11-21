#!/usr/bin/env pwsh
# Tests of image publishing and deployment (E01-03, docs/DEPLOY.md).
#   (no switch)  static, a step of scripts/verify.ps1: base images pinned by digest; the stage compose file uses
#                exactly the published images and validates with its env template; the images workflow builds on
#                PRs and publishes to GHCR on main (sha + latest, release versions), its deploy is gated; the
#                deploy workflow is manual for production and documented; no hosting secret is needed for CI.
#   -Smoke       the real thing on this Docker host, run by the images workflow on every PR: builds the images
#                (or takes them with -NoBuild), deploys the stage compose file with scripts/deploy.ps1, upgrades to
#                another tag, checks that a failing migration stops a deploy and keeps the running version, rolls
#                back, and checks the site, the API's /health/live and the admin through Caddy each time.
param(
    [switch]$Smoke,
    [string]$Registry = 'local/kitabber-smoke',
    [string]$Tag = 'smoke',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $root 'deploy/compose.stage.yaml'
$template = Join-Path $root 'deploy/stage.env.example'
$images = & (Join-Path $PSScriptRoot 'images.ps1') -ListOnly
$problems = [System.Collections.Generic.List[string]]::new()

function Assert($condition, [string]$message) {
    if (-not $condition) { $problems.Add($message) }
}

function Read-Text([string]$Path) { Get-Content -Raw -Encoding utf8 -LiteralPath (Join-Path $root $Path) }

function Read-EnvFile([string]$Path) {
    $values = [ordered]@{}
    foreach ($line in Get-Content -Encoding utf8 -LiteralPath $Path) {
        if ($line -match '^\s*([A-Z][A-Z0-9_]*)=(.*)$') { $values[$Matches[1]] = $Matches[2] }
    }
    $values
}

# The text of one job of a workflow: from "  <name>:" to the next job.
function Get-Job([string]$Workflow, [string]$Name) {
    $m = [regex]::Match($Workflow, "(?ms)^  ${Name}:\s*$.*?(?=^  [a-z][a-z0-9-]*:\s*$|\z)")
    if ($m.Success) { $m.Value } else { '' }
}

function Exit-WithProblems([string]$name) {
    if ($problems.Count) {
        $problems | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
    Write-Host "${name}: OK"
    exit 0
}

if (-not $Smoke) {
    # 1. Base images are pinned by digest (Renovate updates tag and digest together).
    $dockerfiles = @($images.Values | ForEach-Object { $_[0] } | Sort-Object -Unique)
    foreach ($file in $dockerfiles) {
        $text = Read-Text $file
        $args_ = @{}
        foreach ($m in [regex]::Matches($text, '(?m)^ARG ([A-Z_]+)=(\S+)\s*$')) { $args_[$m.Groups[1].Value] = $m.Groups[2].Value }
        $stages = @()
        foreach ($m in [regex]::Matches($text, '(?m)^FROM (\S+)(?: AS (\S+))?\s*$')) {
            $from = $m.Groups[1].Value
            if ($from -match '^\$\{([A-Z_]+)\}$') {
                $value = $args_[$Matches[1]]
                Assert ($value -match '@sha256:[0-9a-f]{64}$') "${file}: base image $from ($value) is not pinned by digest"
            }
            elseif ($stages -notcontains $from) {
                Assert ($from -match '@sha256:[0-9a-f]{64}$') "${file}: base image $from is not pinned by digest"
            }
            if ($m.Groups[2].Success) { $stages += $m.Groups[2].Value }
        }
        foreach ($image in $images.Keys) {
            $f, $target, $null = $images[$image]
            if ($f -eq $file -and $target) { Assert ($stages -contains $target) "${file}: no build target '$target' for image $image" }
        }
        $context = Join-Path $root ($images.Values | Where-Object { $_[0] -eq $file } | Select-Object -First 1)[2]
        Assert (Test-Path -LiteralPath (Join-Path $context '.dockerignore')) "${file}: its build context has no .dockerignore"
    }

    # 2. The stage compose file runs exactly the published images, and its env template covers it.
    $text = Read-Text 'deploy/compose.stage.yaml'
    $own = @()
    foreach ($m in [regex]::Matches($text, '(?m)^\s+image: (.+?)\s*$')) {
        $image = $m.Groups[1].Value
        if ($image -match '^\$\{IMAGE_REGISTRY:\?[^}]+\}/([a-z-]+):\$\{IMAGE_TAG:\?[^}]+\}$') { $own += $Matches[1] }
        else { Assert ($image -match '@sha256:[0-9a-f]{64}$') "deploy/compose.stage.yaml: image $image is not pinned by digest" }
    }
    Assert ((@($own | Sort-Object) -join ',') -eq (@($images.Keys | Sort-Object) -join ',')) "deploy/compose.stage.yaml: runs images '$($own -join ', ')', scripts/images.ps1 builds '$(@($images.Keys) -join ', ')'"
    Assert ($text -notmatch '(?m)^\s+build:') 'deploy/compose.stage.yaml: a stand runs published images, it must not build'
    Assert ($text -notmatch '(?m)^\s+(ASPNETCORE|DOTNET)_ENVIRONMENT: (?!Staging\s*$)') 'deploy/compose.stage.yaml: the hosts must run as Staging'
    $vars = Read-EnvFile $template
    $used = @([regex]::Matches($text, '\$\{([A-Z][A-Z0-9_]*)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    foreach ($name in $used) { Assert ($vars.Contains($name) -or $name -eq 'STAGE_PROJECT') "deploy/stage.env.example: no $name (used in deploy/compose.stage.yaml)" }
    foreach ($name in $vars.Keys) {
        Assert ($used -contains $name) "deploy/stage.env.example: $name is not used in deploy/compose.stage.yaml"
        if ($name -like '*PASSWORD*') { Assert ($vars[$name] -eq 'change-me') "deploy/stage.env.example: $name must be the placeholder change-me" }
    }
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $out = & { $ErrorActionPreference = 'Continue'; docker compose -f $compose --env-file $template config --quiet 2>&1 | Out-String }
        Assert ($LASTEXITCODE -eq 0) "deploy/compose.stage.yaml: docker compose config failed: $out"
        $global:LASTEXITCODE = 0
    }
    elseif ($env:CI) { $problems.Add('CI must validate deploy/compose.stage.yaml: docker is not installed') }
    else { Write-Host '  (docker is not installed: docker compose config of deploy/compose.stage.yaml skipped)' }

    # 3. The deploy script migrates before it replaces the containers.
    $deploy = Read-Text 'scripts/deploy.ps1'
    $migrate = $deploy.IndexOf('run --rm --quiet-pull migrator migrate')
    $up = $deploy.IndexOf('up --detach --wait')
    Assert ($migrate -gt 0 -and $up -gt $migrate) 'scripts/deploy.ps1: must run the migrator, then compose up --wait'

    # 4. The images workflow: builds and smoke-tests on PRs, publishes on main, deploys to stage only when connected.
    $wf = Read-Text '.github/workflows/images.yml'
    Assert ($wf -match '(?m)^  pull_request:') 'images.yml: must run on pull_request'
    Assert ($wf -match '(?ms)^  push:\s*\n\s+branches: \[main\]') 'images.yml: must run on push to main'
    Assert ($wf -match '(?m)^  workflow_call:') 'images.yml: release.yml must be able to call it'
    $build = Get-Job $wf 'build'
    Assert ($build) 'images.yml: no job build'
    Assert ($build -match 'timeout-minutes:\s*\d+') 'images.yml: job build must have a timeout'
    Assert ($build -match 'packages: write') 'images.yml: job build needs packages: write to publish with GITHUB_TOKEN'
    Assert ($build -match 'ghcr\.io/\$\(\$env:GITHUB_REPOSITORY\.ToLowerInvariant\(\)\)') 'images.yml: images must go to ghcr.io/<owner>/<repository>'
    Assert ($build -match '"sha-\$\(\$env:GITHUB_SHA') 'images.yml: every image must be tagged sha-<short sha>'
    Assert ($build -match "GITHUB_EVENT_NAME -eq 'push'\) \{ \`$tags \+= 'latest'") 'images.yml: a merge to main must also tag latest'
    Assert ($build -match '\$version, \(\$version -replace') 'images.yml: a release must tag <version> and <major>.<minor>'
    Assert ($build -match '(?m)run: \./scripts/images\.ps1 .*-Tags') 'images.yml: must build with scripts/images.ps1'
    Assert ($build -match "(?ms)if: github\.event_name != 'pull_request'\s*\n\s+shell: pwsh\s*\n\s+run: \./scripts/images\.ps1 .*-Push") 'images.yml: must publish with scripts/images.ps1 -Push, never on a PR'
    Assert ($build -match '(?m)run: \./scripts/deploy-test\.ps1 -Smoke .*-NoBuild') 'images.yml: must run the deploy smoke test on the built images'
    Assert ($build.IndexOf('deploy-test.ps1 -Smoke') -lt $build.IndexOf('-Push')) 'images.yml: the smoke test must pass before publishing'
    $secrets = @([regex]::Matches($build, 'secrets\.([A-Z_]+)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -ne 'GITHUB_TOKEN' })
    Assert (-not $secrets) "images.yml: job build must need no secret but GITHUB_TOKEN, uses $($secrets -join ', ')"
    $stage = Get-Job $wf 'deploy-stage'
    Assert ($stage -match "(?m)^\s+if: github\.event_name == 'push' && inputs\.version == '' && vars\.DEPLOY_STAGE == 'true'\s*$") "images.yml: deploy-stage must run only on push to main and only when vars.DEPLOY_STAGE is 'true'"
    Assert ($stage -match 'uses: \./\.github/workflows/deploy\.yml' -and $stage -match 'environment: stage') 'images.yml: deploy-stage must call deploy.yml for stage'

    # 5. The deploy workflow: production only by hand, a tag to deploy (an earlier one rolls back), all documented.
    $wf = Read-Text '.github/workflows/deploy.yml'
    Assert ($wf -match '(?ms)^  workflow_dispatch:.*options: \[stage, production\]') 'deploy.yml: workflow_dispatch must offer stage and production'
    Assert ($wf -match '(?ms)^  workflow_dispatch:.*^\s+tag:') 'deploy.yml: workflow_dispatch must take the image tag'
    Assert ($wf -match '(?m)^  workflow_call:') 'deploy.yml: images.yml must be able to call it'
    Assert ($wf -notmatch '(?m)^  (push|pull_request|schedule):') 'deploy.yml: must only run by hand or when called'
    Assert ($wf -match '(?m)^\s+environment: \$\{\{ inputs\.environment \}\}\s*$') 'deploy.yml: the job must use the GitHub environment of the stand'
    Assert ($wf -match '(?m)run: \./scripts/deploy\.ps1 ') 'deploy.yml: must deploy with scripts/deploy.ps1'
    $doc = Read-Text 'docs/DEPLOY.md'
    $names = @([regex]::Matches($wf, '(secrets|vars)\.([A-Z_]+)') | ForEach-Object { $_.Groups[2].Value } | Where-Object { $_ -ne 'GITHUB_TOKEN' } | Sort-Object -Unique)
    $names += 'DEPLOY_STAGE'
    foreach ($name in $names) { Assert ($doc.Contains("``$name``")) "docs/DEPLOY.md: does not describe $name (used by the workflows)" }
    foreach ($word in 'scripts/deploy.ps1', 'compose.stage.yaml', '## Откат', 'rollback <') { Assert ($doc.Contains($word)) "docs/DEPLOY.md: must cover '$word'" }
    foreach ($image in $images.Keys) { Assert ($doc.Contains("/$image``") -or $doc.Contains("``$image``")) "docs/DEPLOY.md: does not name image $image" }

    # 6. A release publishes its version.
    $wf = Read-Text '.github/workflows/release.yml'
    $job = Get-Job $wf 'images'
    Assert ($job -match "if: needs\.release\.outputs\.release_created == 'true'" -and $job -match 'uses: \./\.github/workflows/images\.yml' -and $job -match 'version: \$\{\{ needs\.release\.outputs\.tag_name \}\}' -and $job -match 'packages: write') 'release.yml: a created release must call images.yml with its tag'

    Exit-WithProblems 'deploy config'
}

# ---- Smoke: deploy, upgrade, failed migration, rollback on this Docker host.
$ErrorActionPreference = 'Continue' # native tools write progress to stderr
function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}
function New-Password { -join ((1..24) | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) }) }

$Registry = $Registry.ToLowerInvariant().TrimEnd('/')
$next = "$Tag-next"
$broken = "$Tag-broken"
$temp = Join-Path ([System.IO.Path]::GetTempPath()) "deploy-test-$PID"
New-Item -ItemType Directory -Force $temp | Out-Null
$envFile = Join-Path $temp 'stage.env'
$httpPort = Get-FreePort
do { $httpsPort = Get-FreePort } while ($httpsPort -eq $httpPort)
$project = "kitabber-deploy-smoke-$PID"
Set-Content -Encoding ascii -LiteralPath $envFile @(
    "STAGE_PROJECT=$project", "IMAGE_REGISTRY=$Registry", "IMAGE_TAG=$Tag",
    'SITE_ADDRESS=http://site.localhost', 'ADMIN_ADDRESS=http://admin.localhost',
    "HTTP_PORT=$httpPort", "HTTPS_PORT=$httpsPort",
    "POSTGRES_PASSWORD=$(New-Password)", "REDIS_PASSWORD=$(New-Password)", "RABBITMQ_PASSWORD=$(New-Password)",
    # The workers start without reaching SMTP: a letter would only fail and wait in the queue.
    'SMTP_HOST=smtp.invalid', 'SMTP_PORT=587', 'SMTP_SECURITY=StartTls', 'SMTP_USERNAME=smoke', "SMTP_PASSWORD=$(New-Password)", 'MAIL_FROM=noreply@site.localhost',
    # Sign-in (ADR-0018): keys made for this run only, never committed.
    'SITE_URL=http://site.localhost',
    "IDENTITY_SIGNING_KEY=$([Convert]::ToBase64String([System.Security.Cryptography.RSA]::Create(2048).ExportPkcs8PrivateKey()))",
    "IDENTITY_ENCRYPTION_KEY=$([Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))",
    "SESSION_PASSWORD=$(New-Password)$(New-Password)",
    # The API starts without reaching S3 (ADR-0030): only an upload would, and the smoke test uploads nothing.
    'MEDIA_STORAGE_URL=http://s3.invalid', 'MEDIA_STORAGE_REGION=us-east-1', 'MEDIA_STORAGE_BUCKET=smoke', 'MEDIA_STORAGE_ACCESS_KEY=smoke',
    "MEDIA_STORAGE_SECRET_KEY=$(New-Password)"
)
$dc = @('compose', '-f', $compose, '--env-file', $envFile)
$deployScript = Join-Path $PSScriptRoot 'deploy.ps1'
$shell = (Get-Process -Id $PID).Path

Add-Type -AssemblyName System.Net.Http
$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(15)
function Get-Page([string]$HostName, [string]$Path) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, "http://127.0.0.1:$httpPort$Path")
    $request.Headers.Host = $HostName
    try {
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
    }
    catch { [pscustomobject]@{ Status = 0; Body = "$_" } }
}
function Invoke-Deploy([string]$DeployTag) {
    $out = & $shell -NoProfile -File $deployScript -EnvFile $envFile -Tag $DeployTag -ComposeFile $compose -NoPull 2>&1 | Out-String
    [pscustomobject]@{ Code = $LASTEXITCODE; Output = $out }
}
function Assert-Running([string]$Expected, [string]$When) {
    $states = @{}
    foreach ($line in (docker @dc ps -a --format '{{.Service}}|{{.State}}|{{.Image}}' 2>&1)) {
        $parts = "$line" -split '\|'
        if ($parts.Count -eq 3) { $states[$parts[0]] = $parts }
    }
    foreach ($image in $images.Keys | Where-Object { $_ -ne 'migrator' }) {
        $state = $states[$image]
        Assert ($state -and $state[1] -eq 'running' -and $state[2] -eq "$Registry/${image}:$Expected") "${When}: ${image} should run ${Expected}, got '$($state -join ' ')'"
    }
    Assert (-not $states.ContainsKey('migrator')) "${When}: the migrator container must be removed after it ran"
    $health = Get-Page 'site.localhost' '/health/live'
    Assert ($health.Status -eq 200) "${When}: site /health/live through Caddy is $($health.Status)"
    $site = Get-Page 'site.localhost' '/'
    Assert ($site.Status -eq 200 -and $site.Body -match '<div id="__nuxt"') "${When}: the site through Caddy is $($site.Status), not the Nuxt page"
    $admin = Get-Page 'admin.localhost' '/'
    Assert ($admin.Status -eq 200 -and $admin.Body -match '<div id="app"') "${When}: the admin through Caddy is $($admin.Status), not the admin page"
    $spa = Get-Page 'admin.localhost' '/some/client/route'
    Assert ($spa.Status -eq 200 -and $spa.Body -match '<div id="app"') "${When}: an admin client route must fall back to index.html, got $($spa.Status)"
}
function Stop-WithProblems([string]$When, $Result) {
    $problems.Add("${When}: scripts/deploy.ps1 exited $($Result.Code):`n$(($Result.Output -split "`n" | Select-Object -Last 30) -join "`n")")
    $problems.Add((docker @dc ps -a --format '{{.Service}}: {{.Status}}' 2>&1 | Out-String))
    throw 'deploy failed'
}

$madeTags = @()
try {
    if (-not $NoBuild) {
        & $shell -NoProfile -File (Join-Path $PSScriptRoot 'images.ps1') -Registry $Registry -Tags $Tag
        if ($LASTEXITCODE) { throw 'building the images failed' }
    }
    # Two more versions of the same images: one to upgrade to, one whose migration fails.
    foreach ($image in $images.Keys) {
        docker tag "$Registry/${image}:$Tag" "$Registry/${image}:$next" 2>&1 | Out-Null
        if ($LASTEXITCODE) { throw "no image $Registry/${image}:$Tag (build it first or drop -NoBuild)" }
        $madeTags += "$Registry/${image}:$next"
        if ($image -ne 'migrator') {
            docker tag "$Registry/${image}:$Tag" "$Registry/${image}:$broken" 2>&1 | Out-Null
            $madeTags += "$Registry/${image}:$broken"
        }
    }
    "FROM $Registry/migrator:$Tag`nENTRYPOINT [`"sh`", `"-c`", `"echo migration failed >&2; exit 1`"]" |
        docker build --quiet -t "$Registry/migrator:$broken" - 2>&1 | Out-Null
    if ($LASTEXITCODE) { throw 'building the failing migrator failed' }
    $madeTags += "$Registry/migrator:$broken"

    $result = Invoke-Deploy $Tag
    if ($result.Code) { Stop-WithProblems "first deploy of $Tag" $result }
    Assert-Running $Tag "first deploy of $Tag"

    $result = Invoke-Deploy $next
    if ($result.Code) { Stop-WithProblems "upgrade to $next" $result }
    Assert-Running $next "upgrade to $next"

    $result = Invoke-Deploy $broken
    Assert ($result.Code -ne 0) "deploy of ${broken}: a failing migration must fail the deploy"
    Assert ($result.Output -match 'migration failed') "deploy of ${broken}: the migrator's output must be shown"
    Assert-Running $next "after the failed deploy of $broken"

    $result = Invoke-Deploy $Tag
    if ($result.Code) { Stop-WithProblems "rollback to $Tag" $result }
    Assert-Running $Tag "rollback to $Tag"
}
catch {
    if ("$_" -ne 'deploy failed') { $problems.Add("smoke: $_") }
}
finally {
    docker @dc --profile tools down -v --remove-orphans --timeout 5 2>&1 | Out-Null
    foreach ($ref in $madeTags) { docker image rm $ref 2>&1 | Out-Null }
    $global:LASTEXITCODE = 0
    $http.Dispose()
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}
Exit-WithProblems 'deploy smoke'
