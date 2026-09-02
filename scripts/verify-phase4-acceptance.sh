#!/usr/bin/env bash
set -euo pipefail

dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj \
  --configuration Release \
  --no-build \
  --no-restore \
  --filter-trait "Category=Phase4Acceptance" \
  --max-parallel-test-modules 1
