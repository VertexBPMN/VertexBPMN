[CmdletBinding()]
param(
    [ValidateRange(60, 300)]
    [int]$AccessTokenLifespan = 70,
    [switch]$SecurityAcceptance,
    [switch]$KeepKeycloak
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$keycloakScript = Join-Path $PSScriptRoot "keycloak-oidc-test.ps1"
$apiProject = Join-Path $repositoryRoot "src/VertexBPMN.Api/VertexBPMN.Api.csproj"
$studioProject = Join-Path $repositoryRoot "src/VertexBPMN.Studio/VertexBPMN.Studio.csproj"
$studioPublishDirectory = Join-Path $repositoryRoot "tests/VertexBPMN.Studio.UiTests/TestResults/keycloak-oidc/studio-publish"
$studioPublishedAssembly = Join-Path $studioPublishDirectory "VertexBPMN.Studio.dll"
$apiAssembly = Join-Path $repositoryRoot "src/VertexBPMN.Api/bin/Release/net10.0/VertexBPMN.Api.dll"
$uiTestProject = Join-Path $repositoryRoot "tests/VertexBPMN.Studio.UiTests/VertexBPMN.Studio.UiTests.csproj"
$uiTestAssembly = Join-Path $repositoryRoot "tests/VertexBPMN.Studio.UiTests/bin/Release/net10.0/VertexBPMN.Studio.UiTests.dll"
$resultsDirectory = Join-Path $repositoryRoot "tests/VertexBPMN.Studio.UiTests/TestResults/keycloak-oidc"
$requiredVariables = @(
    "VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD",
    "VERTEXBPMN_KEYCLOAK_DB_PASSWORD",
    "VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET",
    "VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD"
)
$apiProcess = $null
$studioProcess = $null

function Assert-Prerequisites {
    if (-not (Get-Command wslc.exe -ErrorAction SilentlyContinue)) {
        throw "wslc.exe is required for the local Keycloak acceptance test."
    }
    $missing = @($requiredVariables | Where-Object {
        [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
    })
    if ($missing.Count -gt 0) {
        throw "Set the required process secrets before running the test: $($missing -join ', ')."
    }
}

function Wait-ForEndpoint {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 180
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        do {
            if ($Process.HasExited) {
                throw "$Name process exited with code $($Process.ExitCode) before becoming ready."
            }
            try {
                $response = $client.GetAsync($Uri).GetAwaiter().GetResult()
                try {
                    if ([int]$response.StatusCode -in @(200, 302)) { return }
                }
                finally { $response.Dispose() }
            }
            catch [System.Net.Http.HttpRequestException] {
                # Kestrel or Keycloak is still starting.
            }
            Start-Sleep -Milliseconds 500
        } while ([DateTime]::UtcNow -lt $deadline)
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }

    throw "$Name did not become ready at $Uri within $TimeoutSeconds seconds."
}

function Stop-ExactProcess {
    param([System.Diagnostics.Process]$Process)
    if ($null -eq $Process) { return }
    try {
        if (-not $Process.HasExited) {
            $Process.Kill($true)
            $Process.WaitForExit(30000)
        }
    }
    finally { $Process.Dispose() }
}

Assert-Prerequisites
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$apiStdout = Join-Path $resultsDirectory "api.stdout.log"
$apiStderr = Join-Path $resultsDirectory "api.stderr.log"
$studioStdout = Join-Path $resultsDirectory "studio.stdout.log"
$studioStderr = Join-Path $resultsDirectory "studio.stderr.log"
foreach ($artifact in @(
    $apiStdout,
    $apiStderr,
    $studioStdout,
    $studioStderr,
    (Join-Path $resultsDirectory "dependencies.db"),
    (Join-Path $resultsDirectory "dependencies.db-shm"),
    (Join-Path $resultsDirectory "dependencies.db-wal")
)) {
    Remove-Item -LiteralPath $artifact -Force -ErrorAction SilentlyContinue
}

Write-Host "Building API, Studio and the local browser acceptance test..."
& dotnet build $apiProject --configuration Release --no-restore --disable-build-servers --maxcpucount:1
if ($LASTEXITCODE -ne 0) { throw "The API build failed with exit code $LASTEXITCODE." }
& dotnet build $uiTestProject --configuration Release --no-restore --disable-build-servers --maxcpucount:1
if ($LASTEXITCODE -ne 0) { throw "The UI acceptance test build failed with exit code $LASTEXITCODE." }
& dotnet publish $studioProject --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --output $studioPublishDirectory
if ($LASTEXITCODE -ne 0) { throw "The Studio publish failed with exit code $LASTEXITCODE." }

try {
    Write-Host "Starting the dedicated Keycloak test realm..."
    & $keycloakScript -Action Start -AccessTokenLifespan $AccessTokenLifespan -SecurityAcceptance:$SecurityAcceptance

    $env:ASPNETCORE_ENVIRONMENT = "OidcTest"
    $env:DOTNET_ENVIRONMENT = "OidcTest"
    $env:OperationalMode = "OidcTest"
    $env:Database__ApplyMigrationsOnStartup = "true"
    $env:Operational__Metrics__Enabled = "false"
    $env:Runtime__Outbox__Enabled = "false"
    $env:Runtime__Outbox__Provider = "Disabled"
    $env:ConnectionStrings__DependencyRegistry = "Data Source=$resultsDirectory/dependencies.db"
    $env:Jwt__Authority = "http://localhost:58080/realms/vertexbpmn"
    $env:Jwt__Issuer = "http://localhost:58080/realms/vertexbpmn"
    $env:Jwt__Audience = "vertexbpmn-api"
    $env:Jwt__ClockSkewSeconds = "0"
    $env:Jwt__RequireHttpsMetadata = "false"
    $env:Jwt__UseDevelopmentApiKey = "false"

    Write-Host "Starting the real API directly for deterministic browser acceptance..."
    $apiProcess = Start-Process dotnet `
        -ArgumentList @($apiAssembly, "--urls", "http://localhost:51870") `
        -WorkingDirectory (Split-Path -Parent $apiProject) `
        -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $apiStdout -RedirectStandardError $apiStderr
    Wait-ForEndpoint -Uri "http://localhost:51870/api/ready" -Name "VertexBPMN API" -Process $apiProcess

    $env:ApiBaseUrl = "http://localhost:51870/"
    $env:StudioHttpsRedirection__Enabled = "false"
    $env:StudioAuthentication__Authority = "http://localhost:58080/realms/vertexbpmn"
    $env:StudioAuthentication__ClientId = "vertexbpmn-studio"
    $env:StudioAuthentication__ClientSecret = $env:VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET
    $env:StudioAuthentication__ClaimsScope = "vertexbpmn-claims"
    $env:StudioAuthentication__RequireClientSecret = "true"
    $env:StudioAuthentication__RequireHttpsMetadata = "false"
    $env:StudioAuthentication__LocalDevelopmentEnabled = "false"
    $env:StudioAuthentication__UiTestEnabled = "false"

    Write-Host "Starting the real Studio directly for deterministic browser acceptance..."
    $studioProcess = Start-Process dotnet `
        -ArgumentList @($studioPublishedAssembly, "--urls", "http://localhost:5263") `
        -WorkingDirectory $studioPublishDirectory `
        -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $studioStdout -RedirectStandardError $studioStderr
    Wait-ForEndpoint -Uri "http://localhost:5263/" -Name "VertexBPMN Studio" -Process $studioProcess

    $env:VERTEXBPMN_OIDC_TEST_STUDIO_URL = "http://localhost:5263/"
    $env:VERTEXBPMN_OIDC_TEST_API_URL = "http://localhost:51870/"
    $env:VERTEXBPMN_KEYCLOAK_TEST_USER = "vertexbpmn-user"
    $env:VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_ID = "vertexbpmn-security-test"
    $env:VERTEXBPMN_KEYCLOAK_WRONG_AUDIENCE_CLIENT_ID = "vertexbpmn-wrong-audience-test"
    $env:VERTEXBPMN_KEYCLOAK_EXPIRING_CLIENT_ID = "vertexbpmn-expiring-test"
    $env:VERTEXBPMN_KEYCLOAK_ACCESS_TOKEN_LIFESPAN = $AccessTokenLifespan.ToString([Globalization.CultureInfo]::InvariantCulture)
    $env:VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_SECRET = $env:VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET
    $testClasses = @("-class", "*KeycloakOidcLocalAcceptanceTests")
    if ($SecurityAcceptance) {
        $testClasses += @("-class", "*KeycloakOidcSecurityAcceptanceTests")
    }
    Write-Host "Running real browser login, API authorization, refresh and logout$(if ($SecurityAcceptance) { ' plus K05 security acceptance' })..."
    & dotnet $uiTestAssembly @testClasses -parallelMode none
    if ($LASTEXITCODE -ne 0) {
        throw "The Keycloak browser acceptance test failed with exit code $LASTEXITCODE."
    }
}
catch {
    Write-Host "Keycloak OIDC acceptance failed: $($_.Exception.Message)" -ForegroundColor Red
    foreach ($log in @($apiStderr, $apiStdout, $studioStderr, $studioStdout)) {
        if (Test-Path $log) {
            Write-Host "--- $(Split-Path -Leaf $log) ---"
            Get-Content $log -Tail 80
        }
    }
    throw
}
finally {
    Stop-ExactProcess -Process $studioProcess
    Stop-ExactProcess -Process $apiProcess
    if (-not $KeepKeycloak) {
        & $keycloakScript -Action Remove
    }
}
