[CmdletBinding()]
param(
    [string]$Model = "qwen3:8b",
    [string]$OllamaEndpoint = "http://127.0.0.1:11434/",
    [ValidateRange(1, 65535)]
    [int]$PostgresPort = 55439,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$keycloak = Join-Path $PSScriptRoot "keycloak-oidc-test.ps1"
$container = "vertexbpmn-agent-recovery-pg-$([Guid]::NewGuid().ToString('N').Substring(0, 10))"
$postgresVolume = "$container-data"
$databasePassword = [Guid]::NewGuid().ToString("N") + "Aa1!"
$ownsPostgresContainer = $false
$postgresAdminConnection = $null
$saved = @{}
$secretNames = @(
    "VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD", "VERTEXBPMN_KEYCLOAK_DB_PASSWORD",
    "VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET", "VERTEXBPMN_KEYCLOAK_WORKER_CLIENT_SECRET",
    "VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD"
)

function Invoke-Wslc([string[]]$Arguments, [switch]$IgnoreExitCode) {
    $output = & wslc.exe @Arguments 2>&1
    if (-not $IgnoreExitCode -and $LASTEXITCODE -ne 0) {
        throw "wslc failed with exit code $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
    }
}

function Test-PostgresHostBinding([int]$Port) {
    $client = [Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.ConnectAsync([Net.IPAddress]::Loopback, $Port)
        if (-not $connect.Wait([TimeSpan]::FromSeconds(2))) { return $false }
        $stream = $client.GetStream()
        $stream.ReadTimeout = 2000
        $probe = [byte[]](0, 0, 0, 8, 4, 210, 22, 47)
        $stream.Write($probe, 0, $probe.Length)
        $reply = $stream.ReadByte()
        return $reply -eq 83 -or $reply -eq 78
    }
    catch { return $false }
    finally { $client.Dispose() }
}

function Wait-PostgresContainerReady([string]$Name) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & wslc.exe exec $Name pg_isready -U vertexbpmn -d postgres 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Dedicated PostgreSQL did not become ready."
}

try {
    if (-not (Get-Command wslc.exe -ErrorAction SilentlyContinue)) { throw "wslc.exe is required." }
    try { Invoke-RestMethod -Uri ($OllamaEndpoint.TrimEnd('/') + "/api/version") -TimeoutSec 5 | Out-Null }
    catch { throw "Ollama is not reachable at the configured loopback endpoint." }

    foreach ($name in $secretNames) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name)
        if ([string]::IsNullOrWhiteSpace($saved[$name])) {
            [Environment]::SetEnvironmentVariable($name, [Guid]::NewGuid().ToString("N") + "Bb2!")
        }
    }

    # E11 stops PostgreSQL as part of the scenario. Always create a dedicated
    # disposable container so no developer-owned database can be interrupted.
    $ownsPostgresContainer = $true
    Invoke-Wslc @("volume", "create", $postgresVolume)
    Invoke-Wslc @("run", "--detach", "--name", $container,
        "--publish", "127.0.0.1:${PostgresPort}:5432",
        "--env", "POSTGRES_USER=vertexbpmn", "--env", "POSTGRES_PASSWORD=$databasePassword",
        "--env", "POSTGRES_DB=postgres", "--volume", "${postgresVolume}:/var/lib/postgresql/data",
        "postgres:17-alpine")
    Wait-PostgresContainerReady $container
    for ($rebind = 0; $rebind -le 2; $rebind++) {
        if (Test-PostgresHostBinding $PostgresPort) { break }
        if ($rebind -eq 2) { throw "Dedicated PostgreSQL host binding did not answer the protocol probe." }
        Invoke-Wslc @("stop", $container)
        Invoke-Wslc @("start", $container)
        Wait-PostgresContainerReady $container
    }
    $postgresAdminConnection = "Host=127.0.0.1;Port=$PostgresPort;Username=vertexbpmn;Password=$databasePassword;Database=postgres;Pooling=false;Timeout=2;SSL Mode=Disable"

    # This runner generates fresh secrets for every invocation. Remove only its
    # dedicated Keycloak test profile first so a volume initialized with an older
    # random database password can never be reused with the new credentials.
    & $keycloak -Action Remove | Out-Null
    & $keycloak -Action Start
    if ($LASTEXITCODE -ne 0) { throw "Dedicated Keycloak setup failed." }

    if (-not $NoBuild) {
        & dotnet build (Join-Path $root "tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj") `
            -c Release --no-restore --disable-build-servers --maxcpucount:1
        if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    }

    $env:VERTEXBPMN_TEST_POSTGRES_ADMIN = $postgresAdminConnection
    $env:VERTEXBPMN_TEST_POSTGRES_CONTAINER = $container
    $env:VERTEXBPMN_TEST_POSTGRES_VOLUME = $postgresVolume
    $env:VERTEXBPMN_TEST_WORKER_AUTHORITY = "http://localhost:58080/realms/vertexbpmn"
    $env:VERTEXBPMN_TEST_WORKER_CLIENT_SECRET = $env:VERTEXBPMN_KEYCLOAK_WORKER_CLIENT_SECRET
    $env:VERTEXBPMN_TEST_OLLAMA_MODEL = $Model
    $env:VERTEXBPMN_TEST_OLLAMA_ENDPOINT = $OllamaEndpoint
    & dotnet (Join-Path $root "tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll") `
        -class "*ExternalAgentProcessRecoveryAcceptanceTests" -parallelMode none
    if ($LASTEXITCODE -ne 0) { throw "External-agent process recovery acceptance failed." }
}
finally {
    foreach ($name in @("VERTEXBPMN_TEST_POSTGRES_ADMIN",
        "VERTEXBPMN_TEST_POSTGRES_CONTAINER",
        "VERTEXBPMN_TEST_POSTGRES_VOLUME",
        "VERTEXBPMN_TEST_WORKER_AUTHORITY", "VERTEXBPMN_TEST_WORKER_CLIENT_SECRET",
        "VERTEXBPMN_TEST_OLLAMA_MODEL", "VERTEXBPMN_TEST_OLLAMA_ENDPOINT")) {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
    try { & $keycloak -Action Remove | Out-Null } catch { }
    if ($ownsPostgresContainer) {
        Invoke-Wslc @("container", "remove", "--force", $container) -IgnoreExitCode
        Invoke-Wslc @("volume", "remove", $postgresVolume) -IgnoreExitCode
    }
    foreach ($name in $secretNames) {
        [Environment]::SetEnvironmentVariable($name, $saved[$name])
    }
}
