#!/usr/bin/env pwsh
# Tests of image publishing and deployment (E01-03, docs/DEPLOY.md).
#   (no switch)  static, a step of scripts/verify.ps1: base images pinned by digest; the stage compose file uses
#                exactly the published images and validates with its env template; the images workflow builds on
#                PRs and publishes to GHCR on main (sha + latest, release versions), its deploy is gated; the
#                deploy workflow is manual for production and documented; no hosting secret is needed for CI; the
#                telemetry of the stand (ADR-0069) is wired, closed, retained 7-15 days, with valid dashboards.
#   -Smoke       the real thing on this Docker host, run by the images workflow on every PR: builds the images
#                (or takes them with -NoBuild), deploys the stage compose file with scripts/deploy.ps1, upgrades to
#                another tag, checks that a failing migration stops a deploy and keeps the running version, rolls
#                back, and checks the site, the API's /health/live and the admin through Caddy each time.
#                After the first deploy: a request to the API gives a trace, a log line and metrics in Grafana.
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

    # 2b. The telemetry of the stand (ADR-0069): the hosts send OTLP to the collector, only Caddy publishes ports and
    # Grafana is the only telemetry service behind it, with a password; every store keeps 7-15 days; the dashboards
    # are JSON and use the provisioned data sources; the 5xx alert exists.
    function Get-InlineConfig([string]$Name) {
        $m = [regex]::Match($text, "(?m)^  $([regex]::Escape($Name)):\r?\n    content: \|\r?\n((?:(?:      .*)?\r?\n)*)")
        if (-not $m.Success) { $problems.Add("deploy/compose.stage.yaml: no inline config $Name"); return '' }
        (($m.Groups[1].Value -split '\r?\n' | ForEach-Object { if ($_.Length -ge 6) { $_.Substring(6) } else { '' } }) -join "`n").Replace('$$', '$')
    }
    function Get-ComposeService([string]$Name) {
        $m = [regex]::Match($text, "(?ms)^  ${Name}:\s*$.*?(?=^  [a-z][a-z0-9-]*:\s*$|^[a-z]|\z)")
        if ($m.Success) { $m.Value } else { $problems.Add("deploy/compose.stage.yaml: no service $Name"); '' }
    }
    $backend = [regex]::Match($text, '(?ms)^x-backend:.*?(?=^services:)').Value
    Assert ($backend -match '(?m)^    OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317\s*$') 'deploy/compose.stage.yaml: the backend hosts must send OTLP to http://otel-collector:4317'
    Assert (@([regex]::Matches($text, '(?m)^    ports:')).Count -eq 1 -and (Get-ComposeService 'caddy') -match '(?m)^    ports:') 'deploy/compose.stage.yaml: only caddy may publish ports'
    $caddyfile = Get-InlineConfig 'caddyfile'
    Assert ($caddyfile -match '\{\$GRAFANA_ADDRESS\} \{\s*encode\s*reverse_proxy grafana:3000') 'deploy/compose.stage.yaml: Caddy must serve Grafana at GRAFANA_ADDRESS'
    foreach ($inner in 'otel-collector', 'prometheus', 'loki', 'tempo') { Assert ($caddyfile -notmatch "\b${inner}:") "deploy/compose.stage.yaml: Caddy must not expose $inner" }
    $grafana = Get-ComposeService 'grafana'
    Assert ($grafana -match '(?m)^      GF_SECURITY_ADMIN_PASSWORD: \$\{GRAFANA_ADMIN_PASSWORD:\?') 'deploy/compose.stage.yaml: the Grafana admin password must come from GRAFANA_ADMIN_PASSWORD'
    Assert ($grafana -match "(?m)^      GF_AUTH_ANONYMOUS_ENABLED: 'false'" -and $grafana -match "(?m)^      GF_USERS_ALLOW_SIGN_UP: 'false'") 'deploy/compose.stage.yaml: Grafana must allow neither anonymous users nor sign-up'
    function Assert-Days([string]$What, [double]$Days) { Assert ($Days -ge 7 -and $Days -le 15) "deploy/compose.stage.yaml: $What keeps $Days days, not 7-15" }
    $m = [regex]::Match((Get-ComposeService 'prometheus'), '--storage\.tsdb\.retention\.time=(\d+)d')
    if ($m.Success) { Assert-Days 'Prometheus' ([int]$m.Groups[1].Value) } else { $problems.Add('deploy/compose.stage.yaml: Prometheus has no --storage.tsdb.retention.time=<n>d') }
    $loki = Get-InlineConfig 'loki'
    $m = [regex]::Match($loki, '(?m)^  retention_period: (\d+)h\s*$')
    if ($m.Success) { Assert-Days 'Loki' ([int]$m.Groups[1].Value / 24) } else { $problems.Add('deploy/compose.stage.yaml: Loki has no limits_config.retention_period: <n>h') }
    Assert ($loki -match '(?m)^  retention_enabled: true\s*$') 'deploy/compose.stage.yaml: the Loki compactor must apply the retention'
    $m = [regex]::Match((Get-InlineConfig 'tempo'), '(?m)^    block_retention: (\d+)h\s*$')
    if ($m.Success) { Assert-Days 'Tempo' ([int]$m.Groups[1].Value / 24) } else { $problems.Add('deploy/compose.stage.yaml: Tempo has no compactor.compaction.block_retention: <n>h') }
    $collector = Get-InlineConfig 'otel-collector'
    foreach ($pipeline in 'traces: \{ receivers: \[otlp\].*otlp/tempo', 'metrics: \{ receivers: \[otlp\].*otlphttp/prometheus', 'logs: \{ receivers: \[otlp\].*otlphttp/loki') {
        Assert ($collector -match $pipeline) "deploy/compose.stage.yaml: the collector lacks the pipeline $pipeline"
    }
    $rules = Get-InlineConfig 'prometheus-rules'
    Assert ($rules -match '(?m)^\s+- alert: ApiServerErrors\s*$' -and $rules -match 'http_response_status_code=~"5\.\."') 'deploy/compose.stage.yaml: no alert ApiServerErrors on the share of 5xx'
    $datasources = Get-InlineConfig 'grafana-datasources'
    $sources = @([regex]::Matches($datasources, '(?m)^\s+uid: (\S+)\s*$') | ForEach-Object { $_.Groups[1].Value })
    Assert ((@($sources | Sort-Object) -join ',') -eq 'loki,prometheus,tempo') "deploy/compose.stage.yaml: Grafana's data sources are '$($sources -join ', ')', not prometheus, loki and tempo"
    Assert ($datasources.Contains("url: '`$`${__value.raw}'")) 'deploy/compose.stage.yaml: the Loki data source must link a trace id to Tempo (Grafana reads $$ as a literal $)'
    $dashboards = @([regex]::Matches($text, '(?m)^  (dashboard-[a-z]+):\s*$') | ForEach-Object { $_.Groups[1].Value })
    foreach ($name in 'dashboard-api', 'dashboard-dotnet', 'dashboard-database') { Assert ($dashboards -contains $name) "deploy/compose.stage.yaml: no $name" }
    foreach ($name in $dashboards) {
        Assert ($grafana -match "(?m)^      - source: $name\s*\r?\n\s+target: /etc/grafana/dashboards/[a-z]+\.json\s*$") "deploy/compose.stage.yaml: Grafana does not mount $name"
        $json = Get-InlineConfig $name
        try { $board = $json | ConvertFrom-Json -AsHashtable } catch { $problems.Add("deploy/compose.stage.yaml: $name is not JSON: $_"); continue }
        Assert ($board.uid -and $board.title -and $board.panels.Count) "deploy/compose.stage.yaml: $name needs a uid, a title and panels"
        $refs = @([regex]::Matches($json, '"uid": "([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -ne $board.uid })
        foreach ($uid in $refs) { Assert ($sources -contains $uid) "deploy/compose.stage.yaml: $name uses data source $uid, Grafana has '$($sources -join ', ')'" }
        foreach ($panel in $board.panels) { Assert ($panel.datasource -and $panel.targets.Count) "deploy/compose.stage.yaml: ${name}: panel '$($panel.title)' has no data source or query" }
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
$grafanaPassword = New-Password
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
    "MEDIA_STORAGE_SECRET_KEY=$(New-Password)",
    # imgproxy starts without reaching S3 too; the key and salt are hex made for this run (ADR-0037).
    "IMGPROXY_KEY=$(New-Password)", "IMGPROXY_SALT=$(New-Password)",
    # Grafana of the stand (ADR-0069) at its own address behind Caddy, with a password made for this run.
    'GRAFANA_ADDRESS=http://grafana.localhost', "GRAFANA_ADMIN_PASSWORD=$grafanaPassword"
)
$dc = @('compose', '-f', $compose, '--env-file', $envFile)
$deployScript = Join-Path $PSScriptRoot 'deploy.ps1'
$shell = (Get-Process -Id $PID).Path

Add-Type -AssemblyName System.Net.Http
$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(15)
function Get-Page([string]$HostName, [string]$Path, [hashtable]$Headers = @{}) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, "http://127.0.0.1:$httpPort$Path")
    $request.Headers.Host = $HostName
    foreach ($name in $Headers.Keys) { $request.Headers.TryAddWithoutValidation($name, $Headers[$name]) | Out-Null }
    try {
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
    }
    catch { [pscustomobject]@{ Status = 0; Body = "$_" } }
}
function Get-Grafana([string]$Path) {
    $auth = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("admin:$grafanaPassword"))
    Get-Page 'grafana.localhost' $Path @{ Authorization = "Basic $auth" }
}
# The telemetry of the stand (ADR-0069): Grafana asks for a password; a request to the API through Caddy leaves its
# trace in Tempo, its log line in Loki and its metrics in Prometheus, all read through Grafana's data sources; the
# dashboards and the 5xx alert are loaded; Prometheus scrapes the API's /metrics.
function Assert-Telemetry {
    $anonymous = Get-Page 'grafana.localhost' '/api/search'
    Assert ($anonymous.Status -eq 401) "telemetry: Grafana without a password answers $($anonymous.Status), not 401"
    $traceId = -join ((1..32) | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) })
    $spanId = -join ((1..16) | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) })
    $call = Get-Page 'site.localhost' '/api/telemetry-smoke' @{ traceparent = "00-$traceId-$spanId-01" }
    Assert ($call.Status -gt 0) "telemetry: the API did not answer: $($call.Body)"
    $start = [DateTimeOffset]::UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds() * 1000000
    $logQuery = [uri]::EscapeDataString("{service_name=`"platform-api`"} | trace_id=`"$traceId`"")
    $checks = [ordered]@{
        "a trace $traceId in Tempo"                        = { (Get-Grafana "/api/datasources/proxy/uid/tempo/api/traces/$traceId").Status -eq 200 }
        "a log line of trace $traceId in Loki"             = { (Get-Grafana "/api/datasources/proxy/uid/loki/loki/api/v1/query_range?query=$logQuery&start=$start&limit=5").Body -match '"values":\[\[' }
        'the API request metric in Prometheus over OTLP'   = { (Get-Grafana "/api/datasources/proxy/uid/prometheus/api/v1/query?query=$([uri]::EscapeDataString('http_server_request_duration_seconds_count{job="platform-api"}'))").Body -match '"result":\[\{' }
        "the scrape of the API's /metrics"                 = { (Get-Grafana "/api/datasources/proxy/uid/prometheus/api/v1/query?query=$([uri]::EscapeDataString('up{job="platform-api-scrape"} == 1'))").Body -match '"result":\[\{' }
        'the alert ApiServerErrors in Prometheus'          = { (Get-Grafana '/api/datasources/proxy/uid/prometheus/api/v1/rules').Body -match '"name":"ApiServerErrors"' }
        'the dashboards API, .NET runtime and PostgreSQL'  = { $b = (Get-Grafana '/api/search?tag=kitabber').Body; $b -match '"uid":"kitabber-api"' -and $b -match '"uid":"kitabber-dotnet"' -and $b -match '"uid":"kitabber-database"' }
    }
    # The exporters send every few seconds and metrics once a minute: wait up to three minutes.
    $pending = [System.Collections.Generic.List[string]]::new([string[]]@($checks.Keys))
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    while ($pending.Count -and [DateTime]::UtcNow -lt $deadline) {
        foreach ($name in @($pending)) { if (& $checks[$name]) { $pending.Remove($name) | Out-Null } }
        if ($pending.Count) { Start-Sleep -Seconds 5 }
    }
    foreach ($name in $pending) { $problems.Add("telemetry: no $name through Grafana within 3 minutes") }
    if ($pending.Count) { $problems.Add((docker @dc logs --tail 20 otel-collector prometheus loki tempo grafana 2>&1 | Out-String)) }
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
    # imgproxy answers at /img (ADR-0037) and refuses a link without a signature.
    $unsigned = Get-Page 'site.localhost' '/img/insecure/rs:fit:64:0:0/b3JpZ2luYWxzL3g.webp'
    Assert ($unsigned.Status -eq 403) "${When}: an unsigned image link at /img through Caddy is $($unsigned.Status), not 403 from imgproxy"
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
    Assert-Telemetry

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
