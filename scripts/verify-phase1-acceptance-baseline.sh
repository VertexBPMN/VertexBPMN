#!/usr/bin/env bash
set -euo pipefail

configuration="${PHASE1_CONFIGURATION:-Release}"
project="tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj"
dotnet_command="${DOTNET_COMMAND:-dotnet}"

"$dotnet_command" test "$project" \
  --configuration "$configuration" \
  --no-build \
  --no-restore \
  --verbosity minimal \
  --filter-trait "Category=Phase1Acceptance" \
  --max-parallel-test-modules 1

echo "All persistent BPMN core acceptance contracts passed."
