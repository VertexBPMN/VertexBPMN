[CmdletBinding()]
param(
    [ValidateSet("Start", "Stop", "Status", "Bootstrap", "Remove")]
    [string]$Action = "Start",

    [string]$KeycloakImage = "quay.io/keycloak/keycloak:26.7.3",
    [string]$PostgresImage = "postgres:17-alpine",
    [ValidateRange(1, 65535)]
    [int]$KeycloakPort = 58080,
    [ValidateRange(60, 3600)]
    [int]$AccessTokenLifespan = 300,
    [ValidatePattern("^[A-Za-z0-9._~-]+$")]
    [string]$AdminUser = "vertexbpmn-admin",
    [ValidatePattern("^[A-Za-z0-9._~-]+$")]
    [string]$TestUser = "vertexbpmn-user",
    [ValidatePattern("^[A-Za-z0-9._~-]+$")]
    [string]$MfaTestUser = "vertexbpmn-mfa-user"
)

$ErrorActionPreference = "Stop"

$networkName = "vertexbpmn-keycloak-test"
$databaseContainer = "vertexbpmn-keycloak-postgres-test"
$keycloakContainer = "vertexbpmn-keycloak-test"
$databaseVolume = "vertexbpmn-keycloak-postgres-test-data"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$realmFile = Join-Path $repositoryRoot "deploy/keycloak/vertexbpmn-realm.json"
$realmName = "vertexbpmn"
$apiClientId = "vertexbpmn-api"
$studioClientId = "vertexbpmn-studio"
$requiredSecretVariables = @(
    "VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD",
    "VERTEXBPMN_KEYCLOAK_DB_PASSWORD",
    "VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET",
    "VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD"
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

    (Invoke-Wslc -Arguments @($Type, "inspect", $Name) -IgnoreExitCode).ExitCode -eq 0
}

function Test-ContainerRunning {
    param([Parameter(Mandatory)][string]$Name)

    (Invoke-Wslc -Arguments @("exec", $Name, "/bin/true") -IgnoreExitCode).ExitCode -eq 0
}

function Assert-RequiredSecrets {
    $missing = @($requiredSecretVariables | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
    if ($missing.Count -gt 0) {
        throw "Set the following process environment variables before starting the isolated OIDC test profile: $($missing -join ', ')."
    }
}

function Ensure-Network {
    if (-not (Test-WslcObject -Type network -Name $networkName)) {
        $null = Invoke-Wslc -Arguments @("network", "create", $networkName)
    }
}

function Ensure-Volume {
    if (-not (Test-WslcObject -Type volume -Name $databaseVolume)) {
        $null = Invoke-Wslc -Arguments @("volume", "create", $databaseVolume)
    }
}

function Ensure-Container {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$RunArguments
    )

    if (Test-WslcObject -Type container -Name $Name) {
        if (-not (Test-ContainerRunning -Name $Name)) {
            $null = Invoke-Wslc -Arguments @("start", $Name)
        }
        return
    }

    $null = Invoke-Wslc -Arguments (@("run", "--detach", "--name", $Name) + $RunArguments)
}

function Wait-Keycloak {
    $discoveryUri = "http://localhost:$KeycloakPort/realms/$realmName/.well-known/openid-configuration"
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    $restartedForPortBinding = $false
    do {
        try {
            $response = Invoke-RestMethod -Uri $discoveryUri -TimeoutSec 5
            if ($response.issuer -eq "http://localhost:$KeycloakPort/realms/$realmName") {
                return
            }
        }
        catch {
            # WSLC can occasionally leave a freshly published port unbound even
            # after the containerized server reports that it is listening. One
            # bounded restart re-establishes that host binding without touching
            # the dedicated database or any non-Keycloak resources.
            if (-not $restartedForPortBinding -and (Test-ContainerRunning -Name $keycloakContainer)) {
                $startupLogs = Invoke-Wslc -Arguments @("logs", "--tail", "40", $keycloakContainer) -IgnoreExitCode
                if (($startupLogs.Output -join [Environment]::NewLine) -match "Listening on:") {
                    Write-Host "Keycloak is running but its WSLC host port is unavailable; restarting the dedicated container once..."
                    $null = Invoke-Wslc -Arguments @("stop", $keycloakContainer)
                    $null = Invoke-Wslc -Arguments @("start", $keycloakContainer)
                    $restartedForPortBinding = $true
                }
            }
            Start-Sleep -Seconds 2
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    $logs = Invoke-Wslc -Arguments @("logs", "--tail", "80", $keycloakContainer) -IgnoreExitCode
    throw "Keycloak did not publish the expected discovery document within three minutes.`n$($logs.Output -join [Environment]::NewLine)"
}

function Invoke-Kcadm {
    param([Parameter(Mandatory)][string[]]$Arguments)

    Invoke-Wslc -Arguments (@("exec", $keycloakContainer, "/opt/keycloak/bin/kcadm.sh") + $Arguments)
}

function Connect-Kcadm {
    $password = [Environment]::GetEnvironmentVariable("VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD")
    $null = Invoke-Kcadm -Arguments @(
        "config", "credentials",
        "--server", "http://localhost:8080",
        "--realm", "master",
        "--user", $AdminUser,
        "--password", $password
    )
}

function Get-KeycloakEntityId {
    param(
        [Parameter(Mandatory)][string]$Resource,
        [Parameter(Mandatory)][string]$Query
    )

    $result = Invoke-Kcadm -Arguments @("get", $Resource, "-r", $realmName, "-q", $Query)
    $entities = @($result.Output -join [Environment]::NewLine | ConvertFrom-Json)
    if ($entities.Count -ne 1 -or [string]::IsNullOrWhiteSpace($entities[0].id)) {
        throw "Expected exactly one Keycloak $Resource entity for '$Query'."
    }
    $entities[0].id
}

function Ensure-TestUser {
    param(
        [Parameter(Mandatory)][string]$Username,
        [Parameter(Mandatory)][string]$TenantId,
        [Parameter(Mandatory)][string]$Role,
        [switch]$RequireMfaEnrollment
    )

    $lookup = Invoke-Kcadm -Arguments @("get", "users", "-r", $realmName, "-q", "username=$Username")
    $users = @($lookup.Output -join [Environment]::NewLine | ConvertFrom-Json)
    if ($users.Count -eq 0) {
        $null = Invoke-Kcadm -Arguments @(
            "create", "users", "-r", $realmName,
            "-s", "username=$Username",
            "-s", "email=$Username@vertexbpmn.test",
            "-s", "firstName=VertexBPMN",
            "-s", "lastName=Test User",
            "-s", "enabled=true",
            "-s", "emailVerified=true"
        )
    }
    elseif ($users.Count -ne 1) {
        throw "More than one Keycloak user matched '$Username'."
    }

    $userId = Get-KeycloakEntityId -Resource "users" -Query "username=$Username"
    $requiredActions = if ($RequireMfaEnrollment) { '["CONFIGURE_TOTP"]' } else { '[]' }
    $null = Invoke-Kcadm -Arguments @(
        "update", "users/$userId", "-r", $realmName,
        "-s", "email=$Username@vertexbpmn.test",
        "-s", "firstName=VertexBPMN",
        "-s", "lastName=Test User",
        "-s", "emailVerified=true",
        "-s", "attributes.tenant_id=[`"$TenantId`"]",
        "-s", "requiredActions=$requiredActions"
    )

    $password = [Environment]::GetEnvironmentVariable("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD")
    $null = Invoke-Kcadm -Arguments @(
        "set-password", "-r", $realmName,
        "--username", $Username,
        "--new-password", $password
    )
    $null = Invoke-Kcadm -Arguments @(
        "add-roles", "-r", $realmName,
        "--uusername", $Username,
        "--cclientid", $apiClientId,
        "--rolename", $Role
    )
}

function Bootstrap-Realm {
    Assert-RequiredSecrets
    Wait-Keycloak
    Connect-Kcadm

    $null = Invoke-Kcadm -Arguments @(
        "update", "realms/$realmName",
        "-s", "accessTokenLifespan=$AccessTokenLifespan"
    )

    $studioId = Get-KeycloakEntityId -Resource "clients" -Query "clientId=$studioClientId"
    $studioSecret = [Environment]::GetEnvironmentVariable("VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET")
    $null = Invoke-Kcadm -Arguments @(
        "update", "clients/$studioId", "-r", $realmName,
        "-s", "secret=$studioSecret"
    )

    Ensure-TestUser -Username $TestUser -TenantId "tenant-a" -Role "ProcessManager"
    Ensure-TestUser -Username $MfaTestUser -TenantId "tenant-b" -Role "ReadOnly" -RequireMfaEnrollment
}

function Start-IsolatedKeycloak {
    Assert-RequiredSecrets
    Ensure-Network
    Ensure-Volume

    $databasePassword = [Environment]::GetEnvironmentVariable("VERTEXBPMN_KEYCLOAK_DB_PASSWORD")
    Ensure-Container -Name $databaseContainer -RunArguments @(
        "--network", $networkName,
        "--env", "POSTGRES_USER=keycloak",
        "--env", "POSTGRES_PASSWORD=$databasePassword",
        "--env", "POSTGRES_DB=keycloak",
        "--volume", "${databaseVolume}:/var/lib/postgresql/data",
        $PostgresImage
    )

    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        $ready = Invoke-Wslc -Arguments @("exec", $databaseContainer, "pg_isready", "-U", "keycloak", "-d", "keycloak") -IgnoreExitCode
        if ($ready.ExitCode -eq 0) { break }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($ready.ExitCode -ne 0) { throw "The dedicated Keycloak PostgreSQL database did not become ready." }

    $adminPassword = [Environment]::GetEnvironmentVariable("VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD")
    Ensure-Container -Name $keycloakContainer -RunArguments @(
        "--network", $networkName,
        "--publish", "${KeycloakPort}:8080",
        "--env", "KC_BOOTSTRAP_ADMIN_USERNAME=$AdminUser",
        "--env", "KC_BOOTSTRAP_ADMIN_PASSWORD=$adminPassword",
        "--env", "KC_DB=postgres",
        "--env", "KC_DB_URL_HOST=$databaseContainer",
        "--env", "KC_DB_URL_DATABASE=keycloak",
        "--env", "KC_DB_USERNAME=keycloak",
        "--env", "KC_DB_PASSWORD=$databasePassword",
        "--env", "KC_HOSTNAME=http://localhost:$KeycloakPort",
        "--volume", "${realmFile}:/opt/keycloak/data/import/vertexbpmn-realm.json:ro",
        $KeycloakImage,
        "start-dev", "--import-realm", "--http-port=8080", "--health-enabled=true"
    )

    Bootstrap-Realm
    Write-Host "The isolated OIDC test realm is ready at http://localhost:$KeycloakPort/realms/$realmName."
}

function Stop-IsolatedKeycloak {
    foreach ($containerName in @($keycloakContainer, $databaseContainer)) {
        if ((Test-WslcObject -Type container -Name $containerName) -and (Test-ContainerRunning -Name $containerName)) {
            $null = Invoke-Wslc -Arguments @("stop", $containerName)
        }
    }
}

function Remove-IsolatedKeycloak {
    Stop-IsolatedKeycloak

    foreach ($containerName in @($keycloakContainer, $databaseContainer)) {
        if (Test-WslcObject -Type container -Name $containerName) {
            $null = Invoke-Wslc -Arguments @("container", "remove", $containerName)
        }
    }
    if (Test-WslcObject -Type volume -Name $databaseVolume) {
        $null = Invoke-Wslc -Arguments @("volume", "remove", $databaseVolume)
    }
    if (Test-WslcObject -Type network -Name $networkName) {
        $null = Invoke-Wslc -Arguments @("network", "remove", $networkName)
    }
}

function Show-Status {
    foreach ($containerName in @($databaseContainer, $keycloakContainer)) {
        $exists = Test-WslcObject -Type container -Name $containerName
        $running = $exists -and (Test-ContainerRunning -Name $containerName)
        $state = if ($running) { "running" } elseif ($exists) { "stopped" } else { "missing" }
        Write-Host "$containerName`: $state"
    }
}

if (-not (Get-Command wslc.exe -ErrorAction SilentlyContinue)) {
    throw "wslc.exe was not found. This local test profile requires WSL Containers."
}

switch ($Action) {
    "Start" { Start-IsolatedKeycloak }
    "Stop" { Stop-IsolatedKeycloak }
    "Remove" { Remove-IsolatedKeycloak }
    "Status" { Show-Status; return }
    "Bootstrap" { Bootstrap-Realm }
}

Show-Status
