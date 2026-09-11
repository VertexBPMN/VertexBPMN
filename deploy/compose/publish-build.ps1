# ============================================================================
# Publish API + Studio on the HOST (Windows / PowerShell), then (re)build/start
# the docker compose stack. Windows counterpart of publish-build.sh.
#
# Same reasoning as the bash version: the original repo Dockerfiles do a
# solution-wide `dotnet restore VertexBPMN.sln` + publish *inside* the container
# (needs ~5-6 GB free disk and overflows small drives). Publishing on the host
# reuses the local SDK + NuGet cache, then the lean runtime-api.Dockerfile /
# runtime-studio.Dockerfile only COPY the finished output into a slim aspnet image.
#
# Requires: dotnet SDK 10.0.302 (global.json), docker + compose, .env present.
# Does NOT need WSL; runs on native Windows PowerShell 5.1+ / pwsh.
# ============================================================================
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = $PSScriptRoot
$repo = Split-Path (Split-Path $root -Parent) -Parent   # deploy/compose -> repo root

Write-Host "== Host-publish VertexBPMN.Api =="
& dotnet publish (Join-Path $repo 'src\VertexBPMN.Api\VertexBPMN.Api.csproj') `
    -c Release -o (Join-Path $root 'publish\api') /p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (API) failed with exit $LASTEXITCODE" }

Write-Host "== Host-publish VertexBPMN.Studio =="
& dotnet publish (Join-Path $repo 'src\VertexBPMN.Studio\VertexBPMN.Studio.csproj') `
    -c Release -o (Join-Path $root 'publish\studio') /p:UseAppHost=false /p:SkipBpmnIoAssetBuild=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (Studio) failed with exit $LASTEXITCODE" }

Write-Host "== docker compose up -d --build =="
Push-Location $root
try {
    & docker compose up -d --build @args
    if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit $LASTEXITCODE" }
}
finally {
    Pop-Location
}

Write-Host "Done. Studio: http://localhost:5263  |  API ready: http://localhost:51870/api/ready"
