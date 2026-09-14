[CmdletBinding()]
param([switch]$NoBuild)

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
        '*ExternalTaskBoundaryAcceptanceTests', '*ExternalTaskPostgresAcceptanceTests',
        '*ExternalTaskApiContractTests', '*ExternalTaskLeaseServiceTests',
        '*ExternalTaskWorkerServiceTests', '*JwtBearerHandlerOidcTests',
        '*ExternalTaskSchedulingConcurrencyTests', '*ExternalTaskDeploymentTests',
        '*ExternalTaskContractResolverTests', '*ExternalTaskSchedulingPreviewTests',
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
    $arguments += @('-parallelMode', 'none')
    dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Local External Task regression failed: $LASTEXITCODE" }
}
finally { Pop-Location }
