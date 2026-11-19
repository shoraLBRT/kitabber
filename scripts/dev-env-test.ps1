#!/usr/bin/env pwsh
# Tests of the local development environment (E01-04), run as steps of scripts/verify.ps1.
#   (no switch)  static: compose.yaml has every service with a healthcheck, takes ports and passwords only from
#                .env.example, README gives the three commands; no Docker needed.
#   -Smoke       starts the environment under its own project name on free ports, waits for every healthcheck,
#                reaches each service from the host and checks that the passwords from the env file work,
#                then removes the containers and volumes. Needs Docker; CI always runs it.
param([switch]$Smoke)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $root 'compose.yaml'
$template = Join-Path $root '.env.example'
$services = 'postgres', 'redis', 'rabbitmq', 'opensearch', 'minio', 'imgproxy', 'mailpit', 'clickhouse'
$problems = [System.Collections.Generic.List[string]]::new()

function Assert($condition, [string]$message) {
    if (-not $condition) { $problems.Add($message) }
}

function Read-EnvFile([string]$Path) {
    $values = [ordered]@{}
    foreach ($line in Get-Content -Encoding utf8 $Path) {
        if ($line -match '^\s*([A-Z][A-Z0-9_]*)=(.*)$') { $values[$Matches[1]] = $Matches[2] }
    }
    $values
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
    $text = Get-Content -Raw -Encoding utf8 $compose
    $vars = Read-EnvFile $template

    # Every service is there and has a healthcheck.
    $blocks = [regex]::Split(($text -split '(?m)^volumes:')[0], '(?m)^  (?=[a-z][a-z0-9-]*:\s*$)') | Select-Object -Skip 1
    $found = @{}
    foreach ($block in $blocks) { $found[($block -split ':')[0]] = $block }
    foreach ($service in $services) {
        if (-not $found.ContainsKey($service)) { $problems.Add("compose.yaml: no service '$service'"); continue }
        Assert ($found[$service] -match '(?m)^    healthcheck:\s*$') "compose.yaml: service '$service' has no healthcheck"
    }

    # Ports and passwords come from the env file only; every variable has a value in .env.example and is used.
    $used = @([regex]::Matches($text, '\$\{([A-Z][A-Z0-9_]*)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    foreach ($name in $used) { Assert ($vars.Contains($name) -and $vars[$name]) ".env.example: no value for $name (used in compose.yaml)" }
    foreach ($name in $vars.Keys) { Assert ($used -contains $name) ".env.example: $name is not used in compose.yaml" }
    foreach ($m in [regex]::Matches($text, '(?m)^      - (.+:\d+)\s*$')) {
        $value = $m.Groups[1].Value
        if ($value -match '^/|^[a-z]+:/') { continue } # a volume, not a port
        Assert ($value -match '^127\.0\.0\.1:\$\{[A-Z0-9_]+_PORT:\?[^}]+\}:\d+$') "compose.yaml: port '$value' must be 127.0.0.1:`${..._PORT:?...}:<container port>"
    }
    foreach ($m in [regex]::Matches($text, '(?m)^\s+([A-Z_]*(PASSWORD|_PASS|_USER|_KEY|_SALT|_AUTH)):\s*(.+)$')) {
        Assert ($m.Groups[3].Value -match '^\$\{[A-Z0-9_]+:\?') "compose.yaml: $($m.Groups[1].Value) must come from .env (`${NAME:?...})"
    }
    Assert ($vars.Keys.Count -gt 0) '.env.example: no variables'

    # .env stays out of git, the template stays in.
    $ignore = Get-Content -Raw -Encoding utf8 (Join-Path $root '.gitignore')
    Assert ($ignore -match '(?m)^\.env\s*$' -and $ignore -match '(?m)^!\.env\.example\s*$') '.gitignore: must ignore .env and keep .env.example'

    # README: start in three commands.
    $readme = Get-Content -Raw -Encoding utf8 (Join-Path $root 'README.md')
    foreach ($command in 'git clone', 'cp .env.example .env', 'docker compose up -d --wait') {
        Assert ($readme.Contains($command)) "README.md: the start instructions must contain '$command'"
    }

    # With Docker at hand, compose itself must accept the file with the template values.
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $ErrorActionPreference = 'Continue'
        $out = docker compose -f $compose --env-file $template config -q 2>&1 | Out-String
        Assert ($LASTEXITCODE -eq 0) "docker compose config: $out"
        $global:LASTEXITCODE = 0
    }
    Exit-WithProblems 'dev environment config'
}

# ---- Smoke: the real environment.
$ErrorActionPreference = 'Continue' # native tools write progress to stderr
function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) "dev-env-test-$PID"
New-Item -ItemType Directory -Force $temp | Out-Null
$envFile = Join-Path $temp '.env'
$vars = Read-EnvFile $template
$ports = @{}
foreach ($name in @($vars.Keys)) {
    if ($name -like '*_PORT') {
        do { $port = Get-FreePort } while ($ports.Values -contains $port)
        $ports[$name] = $port
        $vars[$name] = "$port"
    }
}
Set-Content -Encoding ascii $envFile ($vars.Keys | ForEach-Object { "$_=$($vars[$_])" })
$project = "platform-verify-$PID"
$dc = @('compose', '-f', $compose, '--env-file', $envFile, '-p', $project)

Add-Type -AssemblyName System.Net.Http
$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(15)
function Get-Status([string]$Url, [hashtable]$Headers = @{}) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $Url)
    foreach ($key in $Headers.Keys) { $request.Headers.TryAddWithoutValidation($key, $Headers[$key]) | Out-Null }
    try { [int]$http.SendAsync($request).GetAwaiter().GetResult().StatusCode } catch { 0 }
}
function Get-Basic([string]$User, [string]$Password) {
    'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${User}:$Password"))
}
function Test-Tcp([int]$Port) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try { $client.ConnectAsync('127.0.0.1', $Port).Wait(5000) -and $client.Connected } catch { $false } finally { $client.Dispose() }
}

try {
    # Pulling is retried: a dropped download is the network, not the environment under test.
    foreach ($attempt in 1..3) {
        $out = docker @dc pull --quiet 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) { break }
        Write-Host "docker compose pull failed (attempt $attempt): $(($out -split "`n" | Select-Object -Last 3) -join ' ')"
    }
    $out = docker @dc up -d --wait --wait-timeout 300 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        $problems.Add("docker compose up -d --wait failed:`n$(($out -split "`n" | Select-Object -Last 30) -join "`n")")
        $problems.Add((docker @dc ps -a --format '{{.Service}}: {{.Status}}' 2>&1 | Out-String))
        throw 'compose up failed'
    }

    # Every service runs and its healthcheck says healthy.
    $states = @{}
    foreach ($line in (docker @dc ps -a --format '{{.Service}}|{{.State}}|{{.Health}}' 2>&1)) {
        $parts = "$line" -split '\|'
        if ($parts.Count -eq 3) { $states[$parts[0]] = $parts }
    }
    foreach ($service in $services) {
        $state = $states[$service]
        Assert ($state -and $state[1] -eq 'running' -and $state[2] -eq 'healthy') "${service}: expected running and healthy, got '$($state -join ' ')'"
    }

    # Reachable from the host on the ports from the env file.
    foreach ($name in 'POSTGRES_PORT', 'REDIS_PORT', 'RABBITMQ_PORT', 'MAILPIT_SMTP_PORT', 'CLICKHOUSE_NATIVE_PORT') {
        Assert (Test-Tcp $ports[$name]) "no TCP connection on $name ($($ports[$name]))"
    }
    $base = 'http://127.0.0.1'
    Assert ((Get-Status "${base}:$($ports.OPENSEARCH_PORT)/_cluster/health") -eq 200) 'opensearch: /_cluster/health is not 200'
    Assert ((Get-Status "${base}:$($ports.MINIO_PORT)/minio/health/live") -eq 200) 'minio: /minio/health/live is not 200'
    Assert ((Get-Status "${base}:$($ports.MINIO_CONSOLE_PORT)/") -eq 200) 'minio: console is not 200'
    Assert ((Get-Status "${base}:$($ports.IMGPROXY_PORT)/health") -eq 200) 'imgproxy: /health is not 200'
    Assert ((Get-Status "${base}:$($ports.MAILPIT_UI_PORT)/readyz") -eq 200) 'mailpit: /readyz is not 200'
    Assert ((Get-Status "${base}:$($ports.CLICKHOUSE_HTTP_PORT)/ping") -eq 200) 'clickhouse: /ping is not 200'

    # The passwords from the env file are the ones the services use, and a wrong one is refused.
    $rabbit = "${base}:$($ports.RABBITMQ_MANAGEMENT_PORT)/api/whoami"
    Assert ((Get-Status $rabbit @{ Authorization = (Get-Basic $vars.RABBITMQ_USER $vars.RABBITMQ_PASSWORD) }) -eq 200) 'rabbitmq: the password from the env file is refused'
    Assert ((Get-Status $rabbit @{ Authorization = (Get-Basic $vars.RABBITMQ_USER 'wrong') }) -eq 401) 'rabbitmq: a wrong password is accepted'
    $click = "${base}:$($ports.CLICKHOUSE_HTTP_PORT)/?query=SELECT%201"
    Assert ((Get-Status $click @{ 'X-ClickHouse-User' = $vars.CLICKHOUSE_USER; 'X-ClickHouse-Key' = $vars.CLICKHOUSE_PASSWORD }) -eq 200) 'clickhouse: the password from the env file is refused'
    Assert ((Get-Status $click @{ 'X-ClickHouse-User' = $vars.CLICKHOUSE_USER; 'X-ClickHouse-Key' = 'wrong' }) -ne 200) 'clickhouse: a wrong password is accepted'
    $minio = docker @dc exec -T minio sh -c "mc alias set probe http://localhost:9000 '$($vars.MINIO_ROOT_USER)' '$($vars.MINIO_ROOT_PASSWORD)' >/dev/null && mc admin info probe >/dev/null && mc mb -q probe/smoke >/dev/null && echo ok" 2>&1 | Out-String
    Assert ($minio.Trim() -eq 'ok') "minio: the password from the env file is refused or the data volume is not writable: $minio"
    $pgQuery = 'psql -h "$(hostname -i)" -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "select 1"'
    $pg = docker @dc exec -T -e "PGPASSWORD=$($vars.POSTGRES_PASSWORD)" postgres sh -c $pgQuery 2>&1 | Out-String
    Assert ($pg.Trim() -eq '1') "postgres: the password from the env file is refused: $pg"
    docker @dc exec -T -e PGPASSWORD=wrong postgres sh -c $pgQuery 2>&1 | Out-Null
    Assert ($LASTEXITCODE -ne 0) 'postgres: a wrong password is accepted'
    $redis = docker @dc exec -T -e "REDISCLI_AUTH=$($vars.REDIS_PASSWORD)" redis redis-cli ping 2>&1 | Out-String
    Assert ($redis.Trim() -eq 'PONG') "redis: the password from the env file is refused: $redis"
    $redis = docker @dc exec -T -e REDISCLI_AUTH=wrong redis redis-cli ping 2>&1 | Out-String
    Assert ($redis.Trim() -ne 'PONG') 'redis: a wrong password is accepted'
}
catch {
    if ("$_" -ne 'compose up failed') { $problems.Add("smoke: $_") }
}
finally {
    docker @dc down -v --remove-orphans --timeout 5 2>&1 | Out-Null
    $global:LASTEXITCODE = 0
    $http.Dispose()
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}
Exit-WithProblems 'dev environment smoke'
