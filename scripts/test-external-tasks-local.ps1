[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$RequireNoSkips,
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:VERTEXBPMN_TEST_POSTGRES_ADMIN)) {
    throw 'Set VERTEXBPMN_TEST_POSTGRES_ADMIN to a local test PostgreSQL instance with database-create permission. No database credentials are logged.'
}

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (-not $NoBuild) {
        dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --no-restore -c Release --disable-build-servers --maxcpucount:1
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    }
    $classes = @(
        '*ExternalTaskBoundaryAcceptanceTests', '*ExternalTaskCompletionAcceptanceTests', '*ExternalTaskPostgresAcceptanceTests',
        '*ExternalTaskApiContractTests', '*ExternalTaskLeaseServiceTests',
        '*ExternalTaskWorkerServiceTests', '*JwtBearerHandlerOidcTests',
        '*ExternalTaskSchedulingConcurrencyTests', '*ExternalTaskDeploymentTests',
        '*ExternalTaskContractResolverTests', '*ExternalTaskSchedulingPreviewTests',
        '*ContractReviewExternalTaskHandlerTests', '*ContractReviewOllamaAcceptanceTests',
        '*ExternalTaskPersistenceTests', '*ExternalTaskDefinitionTests',
        '*PersistentRuntimePhase2AcceptanceTests', '*BpmnCoreLifecycleContractTests',
        '*CompensationSemanticsAcceptanceTests', '*AdvancedFeaturesPhase4AcceptanceTests',
        '*StrictPhaseCSerializerTests', '*StrictPhaseCAdditionalSerializerTests',
        '*UnifiedPhaseFMultiInstanceTests', '*UnifiedParserSerializerTests',
        '*StrictSerializerRoundtripTests', '*DistributedProcessEngineTests',
        '*DistributedProcessEngineTokenLifecycleTests',
        '*ManagementServiceTests'
    )
    $arguments = @('tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll')
    foreach ($class in $classes) { $arguments += @('-class', $class) }
    if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
        $runId = [Guid]::NewGuid().ToString('N')
        $ResultsDirectory = Join-Path (Get-Location) "tests/VertexBPMN.Tests/TestResults/external-tasks/$runId"
    }
    $ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
    $report = Join-Path $ResultsDirectory 'results.xml'
    $arguments += @('-parallelMode', 'none', '-result-xml', $report)
    dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Local External Task regression failed: $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath $report)) { throw "External Task regression produced no XML report: $report" }
    [xml]$result = Get-Content -LiteralPath $report -Raw
    $assemblies = @($result.SelectNodes('/assemblies/assembly'))
    $total = [int](($assemblies | Measure-Object -Property total -Sum).Sum)
    $skipped = [int](($assemblies | Measure-Object -Property skipped -Sum).Sum)
    if ($total -eq 0) { throw "External Task regression discovered no tests. Report: $report" }
    if ($RequireNoSkips -and $skipped -gt 0) {
        throw "External Task acceptance skipped $skipped required test(s). Report: $report"
    }
    Write-Host "External Task regression report: $report (total=$total, skipped=$skipped)"
}
finally { Pop-Location }
