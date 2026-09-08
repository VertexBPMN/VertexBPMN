[CmdletBinding()]
param(
    [ValidateSet('Wslc', 'Existing')][string]$Infrastructure = 'Wslc',
    [string]$PostgresHost = '127.0.0.1',
    [int]$PostgresPort = 55432,
    [string]$RabbitMqHost = '127.0.0.1',
    [int]$RabbitMqPort = 55672,
    [string]$User = 'vertexbpmn',
    [string]$Password = $(if ($env:VERTEXBPMN_WSLC_PASSWORD) { $env:VERTEXBPMN_WSLC_PASSWORD } else { 'vertexbpmn-local' }),
    [switch]$AllowDirty
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'assert-readiness-report.ps1')
$runId = [Guid]::NewGuid().ToString('N')
$output = Join-Path $repo "tests/VertexBPMN.Studio.UiTests/TestResults/production-readiness/$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$summary = [ordered]@{ RunId = $runId; StartedUtc = [DateTime]::UtcNow.ToString('o'); Status = 'Running'; Stages = @() }
$oldPg = $env:VERTEXBPMN_TEST_POSTGRES_ADMIN
$oldRabbit = $env:VERTEXBPMN_TEST_RABBITMQ
$oldBaseline = $env:VERTEXBPMN_UI_BASELINE_TESTS
$oldEditor = $env:VERTEXBPMN_EDITOR_TESTS

function Invoke-Recorded {
    param([string]$Name, [scriptblock]$Action)
    # Full application logs can contain secrets; persist only redacted command output.
    & $Action 2>&1 | ForEach-Object {
        $line = "$_"
        if ($Password) { $line = $line.Replace($Password, '[REDACTED]') }
        $line = $line -replace '(?i)(password|client_secret|access_token|refresh_token|authorization)(\s*[=:]\s*)[^\s,;]+', '$1$2[REDACTED]'
        $line | Out-File (Join-Path $output "$Name.log") -Append -Encoding utf8
    }
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit $LASTEXITCODE); see local report directory." }
}

function Invoke-Suite {
    param([string]$Name, [string]$Project, [string[]]$Filter, [string[]]$Required)
    $suffix = if ($env:OS -eq 'Windows_NT') { '.exe' } else { '' }
    $runner = Join-Path $repo "tests/$Project/bin/Release/net10.0/$Project$suffix"
    $report = Join-Path $output "$Name.xml"
    Write-Host "Running $Name..."
    $started = [DateTime]::UtcNow
    $accepted = $false
    try {
        Invoke-Recorded $Name { & $runner @Filter -parallelMode none -result-xml $report }
        $null = Assert-ReadinessReport -Path $report -RequiredMethods $Required
        $accepted = $true
    }
    finally { Add-SuiteResult $Name $report $started $accepted }
}

function Add-SuiteResult {
    param([string]$Name, [string]$Report, [DateTime]$Started, [bool]$Accepted)
    $stage = [ordered]@{
        Name = $Name; Status = $(if ($Accepted) { 'Passed' } else { 'Failed' })
        StartedUtc = $Started.ToString('o'); DurationSeconds = ([DateTime]::UtcNow - $Started).TotalSeconds
        Report = $Report; Total = $null; Passed = $null; Failed = $null; Skipped = $null; Errors = $null
    }
    try {
        [xml]$xml = Get-Content -LiteralPath $Report -Raw -ErrorAction Stop
        $assemblies = @($xml.SelectNodes('/assemblies/assembly'))
        if ($assemblies.Count -gt 0) {
            foreach ($field in @('Total', 'Passed', 'Failed', 'Skipped', 'Errors')) {
                $stage[$field] = ($assemblies | Measure-Object -Property $field.ToLowerInvariant() -Sum).Sum
            }
        }
    }
    catch { $stage.ReportError = 'Missing or unreadable report; counts are unavailable.' }
    $summary.Stages += $stage
}

Push-Location $repo
try {
    $summary.Commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
    $dirty = @(& git status --porcelain).Count -gt 0
    $summary.Dirty = $dirty
    if ($dirty -and -not $AllowDirty) { throw 'Release qualification requires a clean checkout. Use -AllowDirty only for a diagnostic run.' }
    $summary.Sdk = (& dotnet --version).Trim()
    Invoke-Recorded 'restore' { dotnet restore VertexBPMN.sln }
    Invoke-Recorded 'build' { dotnet build VertexBPMN.sln -c Release --no-restore -m:1 -p:SkipBpmnIoAssetBuild=true }
    $summary.Artifacts = @('src/VertexBPMN.Api', 'src/VertexBPMN.Studio', 'tests/VertexBPMN.Tests', 'tests/VertexBPMN.Studio.UiTests') | ForEach-Object {
        $name = Split-Path $_ -Leaf
        $artifact = Join-Path $repo "$_/bin/Release/net10.0/$name.dll"
        @{ Path = "$_/bin/Release/net10.0/$name.dll"; SHA256 = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash }
    }
    Invoke-Recorded 'packages' { dotnet list VertexBPMN.sln package --include-transitive }
    if ($Infrastructure -eq 'Wslc') {
        & (Join-Path $PSScriptRoot 'wslc-apphost.ps1') -Action Start -InfrastructureOnly -PostgresPort $PostgresPort -RabbitMqPort $RabbitMqPort -User $User -Password $Password
        if ($LASTEXITCODE -ne 0) { throw 'WSLC startup failed.' }
    }
    # Connection strings are used only in process environment, never in the summary.
    $env:VERTEXBPMN_TEST_POSTGRES_ADMIN = "Host=$PostgresHost;Port=$PostgresPort;Username=$User;Password=$Password;Database=postgres"
    $env:VERTEXBPMN_TEST_RABBITMQ = "amqp://$([Uri]::EscapeDataString($User)):$([Uri]::EscapeDataString($Password))@$RabbitMqHost`:$RabbitMqPort/"
    Invoke-Suite 'core' 'VertexBPMN.Tests' @('-trait-', 'Category=Phase3ExternalAcceptance') @()
    Invoke-Suite 'external' 'VertexBPMN.Tests' @('-trait', 'Category=Phase3ExternalAcceptance') @(
        'P3_EXT_01_RabbitMq_health_publish_and_consume_roundtrip',
        'P3_EXT_02_All_EF_migrations_apply_to_real_PostgreSql_databases',
        'P3_EXT_03_Two_isolated_publishers_share_PostgreSql_without_duplicate_leases',
        'P3_EXT_04_RabbitMq_rejects_unroutable_mandatory_delivery',
        'FreshAndUpgradedDatabase_PreserveUtcStatesAndDeleteOnlyExpired')
    $env:VERTEXBPMN_UI_BASELINE_TESTS = 'true'
    $env:VERTEXBPMN_EDITOR_TESTS = '1'
    Invoke-Suite 'browser' 'VertexBPMN.Studio.UiTests' @('-trait-', 'Category=LocalStudioE2E') @(
        'BpmnModeler_Getraenkeabwicklung_LoadsEditsExportsAndReimports_InBrowser')
    $e2e = Join-Path $output 'e2e'
    $e2eStarted = [DateTime]::UtcNow
    $e2eAccepted = $false
    try {
        Invoke-Recorded 'e2e' { & (Join-Path $PSScriptRoot 'test-studio-e2e.ps1') -Infrastructure $Infrastructure -PostgresHost $PostgresHost -PostgresPort $PostgresPort -RabbitMqHost $RabbitMqHost -RabbitMqPort $RabbitMqPort -User $User -Password $Password -SkipBuild -ResultsDirectory $e2e }
        $null = Assert-ReadinessReport (Join-Path $e2e 'results.xml') @(
        'BpmnModeler_ImportsEditsDeploysReloadsAndExports_ARealPersistedDefinition',
        'DmnModeler_ImportsDeploysReloadsEvaluatesAndExports_ARealDecision',
        'FormBuilder_ImportsSavesReloadsAndExports_ARealTenantForm',
        'CmmnModeler_ImportsRegistersExecutesUpdatesAndExports_ARealCase',
        'BpmnRuntime_StartsClaimsCompletesAndShowsPersistedHistory_WithARealTaskForm')
        $e2eAccepted = $true
    }
    finally { Add-SuiteResult 'e2e' (Join-Path $e2e 'results.xml') $e2eStarted $e2eAccepted }
    if ((& git rev-parse HEAD).Trim() -ne $summary.Commit) { throw 'Source commit changed during qualification.' }
    if (-not $AllowDirty -and @(& git status --porcelain).Count -gt 0) { throw 'Working tree changed during qualification.' }
    $summary.Status = if ($AllowDirty) { 'DiagnosticPassed' } else { 'AcceptancePassed' }
}
catch {
    $summary.Status = 'Failed'
    $summary.Error = 'Qualification failed. Inspect the local stage reports; no release approval.'
    throw
}
finally {
    $summary.FinishedUtc = [DateTime]::UtcNow.ToString('o')
    $summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $output 'summary.json') -Encoding utf8
    $env:VERTEXBPMN_TEST_POSTGRES_ADMIN = $oldPg
    $env:VERTEXBPMN_TEST_RABBITMQ = $oldRabbit
    $env:VERTEXBPMN_UI_BASELINE_TESTS = $oldBaseline
    $env:VERTEXBPMN_EDITOR_TESTS = $oldEditor
    Pop-Location
    Write-Host "Readiness status: $($summary.Status). Report: $output"
}
