[CmdletBinding()]
param(
    [ValidateSet("Auto", "Wslc", "Existing")]
    [string]$Infrastructure = "Auto",

    [string]$PostgresHost = "127.0.0.1",
    [ValidateRange(1, 65535)]
    [int]$PostgresPort = 55432,
    [string]$RabbitMqHost = "127.0.0.1",
    [ValidateRange(1, 65535)]
    [int]$RabbitMqPort = 55672,
    [ValidatePattern("^[A-Za-z0-9._~-]+$")]
    [string]$User = "vertexbpmn",
    [ValidatePattern("^[A-Za-z0-9._~-]+$")]
    [string]$Password = $(if ($env:VERTEXBPMN_WSLC_PASSWORD) { $env:VERTEXBPMN_WSLC_PASSWORD } else { "vertexbpmn-local" }),

    [string]$TestMethod
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repositoryRoot "tests/VertexBPMN.Studio.UiTests/VertexBPMN.Studio.UiTests.csproj"
$apiProject = Join-Path $repositoryRoot "src/VertexBPMN.Api/VertexBPMN.Api.csproj"
$wslcScript = Join-Path $PSScriptRoot "wslc-apphost.ps1"
$runId = [Guid]::NewGuid().ToString("N")
$artifactsDirectory = Join-Path $repositoryRoot "tests/VertexBPMN.Studio.UiTests/TestResults/studio-e2e/$runId"

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

function Invoke-LocalTestRunner {
    $runnerName = if ($IsWindows -or $env:OS -eq "Windows_NT") { "VertexBPMN.Studio.UiTests.exe" } else { "VertexBPMN.Studio.UiTests" }
    $runner = Join-Path $repositoryRoot "tests/VertexBPMN.Studio.UiTests/bin/Release/net10.0/$runnerName"
    if (-not (Test-Path -LiteralPath $runner)) {
        throw "The local xUnit runner was not produced at '$runner'."
    }

    New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null
    $report = Join-Path $artifactsDirectory "results.html"
    $xmlReport = Join-Path $artifactsDirectory "results.xml"
    $runnerArguments = @("-trait", "Category=LocalStudioE2E", "-parallelMode", "none", "-longRunning", "30", "-showLiveOutput", "-result-html", $report, "-result-xml", $xmlReport)
    if (-not [string]::IsNullOrWhiteSpace($TestMethod)) {
        $methodFilter = if ($TestMethod.Contains(".")) { $TestMethod } else { "VertexBPMN.Studio.UiTests.LocalStudioInfrastructureTests.$TestMethod" }
        $runnerArguments += @("-method", $methodFilter)
    }
    & $runner @runnerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Local Studio E2E tests failed with exit code $LASTEXITCODE. Report: $report"
    }
    if (-not (Test-Path -LiteralPath $xmlReport)) {
        throw "Local Studio E2E runner did not produce the expected XML report: $xmlReport"
    }

    [xml]$result = Get-Content -LiteralPath $xmlReport -Raw
    $total = [int](($result.assemblies.assembly | Measure-Object -Property total -Sum).Sum)
    if ($total -eq 0) {
        throw "Local Studio E2E filter discovered zero tests. Method filter: '$TestMethod'. Report: $report"
    }
}

function Test-TcpEndpoint {
    param(
        [Parameter(Mandatory)][string]$HostName,
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][string]$Description
    )

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.ConnectAsync($HostName, $Port)
        if (-not $connect.Wait([TimeSpan]::FromSeconds(5))) {
            throw "$Description did not accept a TCP connection at $HostName`:$Port within five seconds."
        }
        $null = $connect.GetAwaiter().GetResult()
    }
    catch {
        throw "$Description is not reachable at $HostName`:$Port. $($_.Exception.Message)"
    }
    finally {
        $client.Dispose()
    }
}

$effectiveInfrastructure = $Infrastructure
if ($effectiveInfrastructure -eq "Auto") {
    $effectiveInfrastructure = if (Get-Command wslc.exe -ErrorAction SilentlyContinue) { "Wslc" } else { "Existing" }
}

Write-Host "Building the real API, Studio and local browser test host before infrastructure startup..."
Invoke-DotNet -Arguments @("build", $apiProject, "--configuration", "Release", "--nologo", "--disable-build-servers", "--maxcpucount:1")
Invoke-DotNet -Arguments @("build", $testProject, "--configuration", "Release", "--nologo", "--disable-build-servers", "--maxcpucount:1")

if ($effectiveInfrastructure -eq "Wslc") {
    if (-not (Get-Command wslc.exe -ErrorAction SilentlyContinue)) {
        throw "WSLC mode was selected, but wslc.exe is not installed or not available on PATH."
    }

    Write-Host "Ensuring local PostgreSQL and RabbitMQ through WSLC..."
    & $wslcScript -Action Start -InfrastructureOnly -PostgresPort $PostgresPort -RabbitMqPort $RabbitMqPort -User $User -Password $Password
    if ($LASTEXITCODE -ne 0) {
        throw "WSLC infrastructure startup failed with exit code $LASTEXITCODE."
    }
}
else {
    Write-Host "Using existing local PostgreSQL and RabbitMQ installations."
}

Test-TcpEndpoint -HostName $PostgresHost -Port $PostgresPort -Description "PostgreSQL"
Test-TcpEndpoint -HostName $RabbitMqHost -Port $RabbitMqPort -Description "RabbitMQ"

$postgresBase = "Host=$PostgresHost;Port=$PostgresPort;Username=$User;Password=$Password;SSL Mode=Disable;Timeout=10;Command Timeout=30"
$settings = [ordered]@{
    VERTEXBPMN_STUDIO_E2E_ENABLED = "true"
    VERTEXBPMN_STUDIO_E2E_RUN_ID = $runId
    VERTEXBPMN_STUDIO_E2E_ARTIFACTS = $artifactsDirectory
    VERTEXBPMN_E2E_BPMN_CONNECTION = "$postgresBase;Database=vertexbpmn_bpmn"
    VERTEXBPMN_E2E_TENANT_CONNECTION = "$postgresBase;Database=vertexbpmn_tenants"
    VERTEXBPMN_E2E_SIMULATION_CONNECTION = "$postgresBase;Database=vertexbpmn_simulation"
    VERTEXBPMN_E2E_EVENTS_CONNECTION = "$postgresBase;Database=vertexbpmn_events"
    VERTEXBPMN_E2E_DECISION_CONNECTION = "$postgresBase;Database=vertexbpmn_decision"
    VERTEXBPMN_E2E_RABBITMQ_CONNECTION = "amqp://$User`:$Password@$RabbitMqHost`:$RabbitMqPort/"
    VERTEXBPMN_STUDIO_E2E_WSLC_POSTGRES_CONTAINER = $(if ($effectiveInfrastructure -eq "Wslc") { "vertexbpmn-postgres" } else { "" })
}
$previousSettings = @{}

try {
    foreach ($entry in $settings.GetEnumerator()) {
        $previousSettings[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key)
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
    }

    Write-Host "Running local real Studio E2E infrastructure tests (run $runId)..."
    Invoke-LocalTestRunner
}
finally {
    foreach ($entry in $previousSettings.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
    }
}
