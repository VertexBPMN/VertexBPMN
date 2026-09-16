[CmdletBinding()]
param(
    [switch]$NoBuild,
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:VERTEXBPMN_TEST_OLLAMA_MODEL)) {
    throw 'Set VERTEXBPMN_TEST_OLLAMA_MODEL to the exact model declared in the approved benchmark manifest.'
}

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (-not $NoBuild) {
        dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --no-restore -c Release --disable-build-servers --maxcpucount:1
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    }
    if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
        $runId = [Guid]::NewGuid().ToString('N')
        $ResultsDirectory = Join-Path (Get-Location) "tests/VertexBPMN.Tests/TestResults/contract-review-benchmark/$runId"
    }
    $ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
    $env:VERTEXBPMN_TEST_CONTRACT_BENCHMARK_RESULTS = $ResultsDirectory
    $report = Join-Path $ResultsDirectory 'results.xml'
    dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll `
        -class '*ContractReviewBenchmarkTests' -parallelMode none -result-xml $report
    if ($LASTEXITCODE -ne 0) { throw "Contract review benchmark failed: $LASTEXITCODE. Report: $report" }
    if (-not (Test-Path -LiteralPath $report)) { throw "Benchmark produced no xUnit report: $report" }
    Write-Host "Contract review benchmark reports: $ResultsDirectory"
}
finally {
    Remove-Item Env:VERTEXBPMN_TEST_CONTRACT_BENCHMARK_RESULTS -ErrorAction SilentlyContinue
    Pop-Location
}
