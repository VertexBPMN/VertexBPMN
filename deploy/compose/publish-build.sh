#!/usr/bin/env bash
# Publish API + Studio on the HOST, then (re)build/start the compose stack.
#
# Why host-publish? The original repo Dockerfiles do a solution-wide
# `dotnet restore VertexBPMN.sln` + publish *inside* the container, which needs
# ~5-6 GB of free disk and overflows the 29 GB root partition on the reference
# host. Publishing on the host reuses the local SDK + NuGet cache (writes to the
# host disk), then the lean `runtime-api.Dockerfile` / `runtime-studio.Dockerfile`
# only COPY the finished output into a slim aspnet image.
#
# Requires: dotnet SDK 10.0.302 (global.json), docker + compose, .env present.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "== Host-publish VertexBPMN.Api =="
dotnet publish "../../src/VertexBPMN.Api/VertexBPMN.Api.csproj" \
    -c Release -o publish/api /p:UseAppHost=false

echo "== Host-publish VertexBPMN.Studio =="
dotnet publish "../../src/VertexBPMN.Studio/VertexBPMN.Studio.csproj" \
    -c Release -o publish/studio /p:UseAppHost=false /p:SkipBpmnIoAssetBuild=true

echo "== docker compose up -d --build =="
docker compose up -d --build "$@"
