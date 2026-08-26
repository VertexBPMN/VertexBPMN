#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
nuget_report="$(mktemp)"
npm_report="$(mktemp)"

cleanup() {
  rm -f "$nuget_report" "$npm_report"
}
trap cleanup EXIT

cd "$root_dir"
dotnet package list \
  --project VertexBPMN.sln \
  --vulnerable \
  --include-transitive \
  --format json \
  --output-version 1 \
  --no-restore >"$nuget_report"

nuget_vulnerabilities="$(jq '[.. | objects | .vulnerabilities? // empty | .[]] | length' "$nuget_report")"
if [[ "$nuget_vulnerabilities" -ne 0 ]]; then
  jq '.. | objects | select(has("vulnerabilities")) | {framework, topLevelPackages, transitivePackages}' "$nuget_report"
  echo "NuGet audit failed: $nuget_vulnerabilities vulnerable package entries found." >&2
  exit 1
fi

cd "$root_dir/src/VertexBPMN.Studio"
if ! npm audit --audit-level=high --json >"$npm_report"; then
  cat "$npm_report" >&2
  echo "npm audit failed: high or critical vulnerabilities found." >&2
  exit 1
fi

npm_high="$(jq '.metadata.vulnerabilities.high // 0' "$npm_report")"
npm_critical="$(jq '.metadata.vulnerabilities.critical // 0' "$npm_report")"
if [[ "$npm_high" -ne 0 || "$npm_critical" -ne 0 ]]; then
  cat "$npm_report" >&2
  echo "npm audit failed: high=$npm_high, critical=$npm_critical." >&2
  exit 1
fi

echo "Dependency audit passed: NuGet=0 vulnerable entries, npm high=0, npm critical=0."
