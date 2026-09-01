[CmdletBinding()]
param(
    [ValidateSet("Start", "Stop", "Status")]
    [string]$Action = "Start",

    [string]$PostgresImage = "postgres:17-alpine",
    [string]$RabbitMqImage = "rabbitmq:4-management-alpine",
    [ValidatePattern("^[A-Za-z0-9._~-]+$")]
    [string]$User = "vertexbpmn",
    [ValidatePattern("^[A-Za-z0-9._~-]+$")]
    [string]$Password = $(if ($env:VERTEXBPMN_WSLC_PASSWORD) { $env:VERTEXBPMN_WSLC_PASSWORD } else { "vertexbpmn-local" }),
    [ValidateRange(1, 65535)]
    [int]$PostgresPort = 55432,
    [ValidateRange(1, 65535)]
    [int]$RabbitMqPort = 55672,
    [ValidateRange(1, 65535)]
    [int]$RabbitMqManagementPort = 15673,
    [switch]$ExistingInfrastructure,
    [switch]$InfrastructureOnly
)

$ErrorActionPreference = "Stop"

$networkName = "vertexbpmn"
$postgresContainer = "vertexbpmn-postgres"
$rabbitMqContainer = "vertexbpmn-rabbitmq"
$postgresVolume = "vertexbpmn-postgres-data"
$rabbitMqVolume = "vertexbpmn-rabbitmq-data"
$databaseNames = @(
    "vertexbpmn_bpmn",
    "vertexbpmn_tenants",
    "vertexbpmn_simulation",
    "vertexbpmn_events",
    "vertexbpmn_decision"
)

function Invoke-Wslc {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [switch]$IgnoreExitCode
    )

    $output = & wslc.exe @Arguments 2>&1
    if (-not $IgnoreExitCode -and $LASTEXITCODE -ne 0) {
        throw "wslc command failed with exit code $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
    }

    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $output
    }
}

function Test-WslcObject {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("container", "network", "volume")]
        [string]$Type,
        [Parameter(Mandatory)]
        [string]$Name
    )

    $result = Invoke-Wslc -Arguments @($Type, "inspect", $Name) -IgnoreExitCode
    return $result.ExitCode -eq 0
}

function Test-ContainerRunning {
    param([Parameter(Mandatory)][string]$Name)

    $result = Invoke-Wslc -Arguments @("exec", $Name, "/bin/true") -IgnoreExitCode
    return $result.ExitCode -eq 0
}

function Ensure-Network {
    if (-not (Test-WslcObject -Type network -Name $networkName)) {
        Write-Host "Creating WSLC network '$networkName'..."
        $null = Invoke-Wslc -Arguments @("network", "create", $networkName)
    }
}

function Ensure-Volume {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Test-WslcObject -Type volume -Name $Name)) {
        Write-Host "Creating WSLC volume '$Name'..."
        $null = Invoke-Wslc -Arguments @("volume", "create", $Name)
    }
}

function Initialize-RabbitMqVolume {
    # WSLC initializes the image volume before the RabbitMQ entry point runs and
    # may leave .erlang.cookie owned by root. Normalize ownership in a short-lived
    # init container before starting the long-running broker.
    $null = Invoke-Wslc -Arguments @(
        "run", "--rm",
        "--user", "0",
        "--volume", "${rabbitMqVolume}:/var/lib/rabbitmq",
        "--entrypoint", "sh",
        $RabbitMqImage,
        "-c", "chown -R rabbitmq:rabbitmq /var/lib/rabbitmq && chmod 700 /var/lib/rabbitmq"
    )
}

function Ensure-Container {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$RunArguments
    )

    if (Test-WslcObject -Type container -Name $Name) {
        if (-not (Test-ContainerRunning -Name $Name)) {
            Write-Host "Starting existing WSLC container '$Name'..."
            $null = Invoke-Wslc -Arguments @("start", $Name)
        }
        return
    }

    Write-Host "Creating WSLC container '$Name'..."
    $null = Invoke-Wslc -Arguments (@("run", "--detach", "--name", $Name) + $RunArguments)
}

function Wait-ForContainer {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$Probe,
        [int]$TimeoutSeconds = 90
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $result = Invoke-Wslc -Arguments (@("exec", $Name) + $Probe) -IgnoreExitCode
        if ($result.ExitCode -eq 0) {
            return
        }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)

    $logs = Invoke-Wslc -Arguments @("logs", "--tail", "40", $Name) -IgnoreExitCode
    throw "Container '$Name' did not become ready within $TimeoutSeconds seconds.`n$($logs.Output -join [Environment]::NewLine)"
}

function Start-Infrastructure {
    Ensure-Network
    Ensure-Volume -Name $postgresVolume
    Ensure-Volume -Name $rabbitMqVolume
    Initialize-RabbitMqVolume

    Ensure-Container -Name $postgresContainer -RunArguments @(
        "--network", $networkName,
        "--publish", "${PostgresPort}:5432",
        "--env", "POSTGRES_USER=$User",
        "--env", "POSTGRES_PASSWORD=$Password",
        "--env", "POSTGRES_DB=postgres",
        "--volume", "${postgresVolume}:/var/lib/postgresql/data",
        $PostgresImage
    )
    Ensure-Container -Name $rabbitMqContainer -RunArguments @(
        "--user", "0",
        "--network", $networkName,
        "--publish", "${RabbitMqPort}:5672",
        "--publish", "${RabbitMqManagementPort}:15672",
        "--env", "RABBITMQ_DEFAULT_USER=$User",
        "--env", "RABBITMQ_DEFAULT_PASS=$Password",
        "--volume", "${rabbitMqVolume}:/var/lib/rabbitmq",
        $RabbitMqImage
    )

    Write-Host "Waiting for PostgreSQL and RabbitMQ..."
    Wait-ForContainer -Name $postgresContainer -Probe @("pg_isready", "-U", $User, "-d", "postgres")
    Wait-ForContainer -Name $rabbitMqContainer -Probe @("rabbitmq-diagnostics", "-q", "ping")

    foreach ($databaseName in $databaseNames) {
        $query = "SELECT 1 FROM pg_database WHERE datname='$databaseName'"
        $exists = Invoke-Wslc -Arguments @(
            "exec", $postgresContainer, "psql", "-U", $User, "-d", "postgres", "-tAc", $query
        )
        if (($exists.Output -join "").Trim() -ne "1") {
            Write-Host "Creating PostgreSQL database '$databaseName'..."
            $null = Invoke-Wslc -Arguments @(
                "exec", $postgresContainer, "createdb", "-U", $User, $databaseName
            )
        }
    }
}

function Stop-Infrastructure {
    foreach ($containerName in @($rabbitMqContainer, $postgresContainer)) {
        if ((Test-WslcObject -Type container -Name $containerName) -and (Test-ContainerRunning -Name $containerName)) {
            Write-Host "Stopping WSLC container '$containerName'..."
            $null = Invoke-Wslc -Arguments @("stop", $containerName)
        }
    }
}

function Show-Status {
    foreach ($containerName in @($postgresContainer, $rabbitMqContainer)) {
        $exists = Test-WslcObject -Type container -Name $containerName
        $running = $exists -and (Test-ContainerRunning -Name $containerName)
        $status = if ($running) { "running" } elseif ($exists) { "stopped" } else { "missing" }
        Write-Host "$containerName`: $status"
    }
}

if (-not $ExistingInfrastructure -and -not (Get-Command wslc.exe -ErrorAction SilentlyContinue)) {
    throw "wslc.exe was not found. Install or update WSL Containers before using this profile."
}

if ($ExistingInfrastructure -and ($Action -ne "Start" -or $InfrastructureOnly)) {
    throw "Existing infrastructure is not managed by this script. Use only -Action Start without -InfrastructureOnly."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$appHostProject = Join-Path $repositoryRoot "src/VertexBPMN.AppHost/VertexBPMN.AppHost.csproj"
if ($Action -eq "Start" -and -not $InfrastructureOnly) {
    # Build before the first wslc.exe invocation. WSLC 2.9.3 can leave the
    # calling process in a state where a subsequent MSBuild exits with code 1
    # without reporting a compiler error.
    Write-Host "Building VertexBPMN.AppHost..."
    & dotnet build $appHostProject --nologo --no-restore --disable-build-servers --maxcpucount:1
    if ($LASTEXITCODE -ne 0) {
        throw "VertexBPMN.AppHost build failed with exit code $LASTEXITCODE."
    }
}

if ($ExistingInfrastructure) {
    Write-Host "Using existing PostgreSQL and RabbitMQ services; no WSLC commands will be executed."
}
else {
    switch ($Action) {
        "Stop" {
            Stop-Infrastructure
            return
        }
        "Status" {
            Show-Status
            return
        }
        "Start" {
            Start-Infrastructure
        }
    }

    Show-Status
    if ($InfrastructureOnly) {
        return
    }
}

$postgresBase = "Host=localhost;Port=$PostgresPort;Username=$User;Password=$Password"

Write-Host "Starting VertexBPMN.AppHost with external local infrastructure..."
$env:DOTNET_ENVIRONMENT = "Wslc"
$env:ASPNETCORE_ENVIRONMENT = "Wslc"
$env:VertexBPMN__ApiHostingMode = "ExternalServices"
$env:ConnectionStrings__BpmnDbContext = "$postgresBase;Database=vertexbpmn_bpmn"
$env:ConnectionStrings__TenantDbContext = "$postgresBase;Database=vertexbpmn_tenants"
$env:ConnectionStrings__SimulationScenarioDbContext = "$postgresBase;Database=vertexbpmn_simulation"
$env:ConnectionStrings__ProcessMiningEvents = "$postgresBase;Database=vertexbpmn_events"
$env:ConnectionStrings__DecisionDbContext = "$postgresBase;Database=vertexbpmn_decision"
$env:ConnectionStrings__messaging = "amqp://$User`:$Password@localhost`:$RabbitMqPort/"

& dotnet run --project $appHostProject --no-launch-profile --no-build
exit $LASTEXITCODE
