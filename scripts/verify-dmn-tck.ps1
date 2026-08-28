param(
    [string]$TckRoot = $env:DMN_TCK_ROOT
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pinnedRevision = (Get-Content (Join-Path $repositoryRoot 'eng/dmn-tck.version') -Raw).Trim()
$temporaryCheckout = $null

try {
    if ([string]::IsNullOrWhiteSpace($TckRoot)) {
        $temporaryCheckout = Join-Path ([System.IO.Path]::GetTempPath()) "vertexbpmn-dmn-tck-$pinnedRevision"
        if (-not (Test-Path -LiteralPath $temporaryCheckout)) {
            git clone --filter=blob:none --no-checkout https://github.com/dmn-tck/tck.git $temporaryCheckout
            git -C $temporaryCheckout checkout $pinnedRevision -- TestCases
        }
        $TckRoot = Join-Path $temporaryCheckout 'TestCases'
    }

    dotnet run --project (Join-Path $repositoryRoot 'tests/VertexBPMN.DmnTckRunner/VertexBPMN.DmnTckRunner.csproj') --configuration Release -- $TckRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    # A shared pinned checkout is intentionally retained in the OS temp folder for repeatable local runs.
}
