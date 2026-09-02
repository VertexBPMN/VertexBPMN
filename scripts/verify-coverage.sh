#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
coverage_file="$(mktemp --suffix=.cobertura.xml)"
minimum_line="${VERTEXBPMN_MIN_LINE_COVERAGE:-60}"
minimum_branch="${VERTEXBPMN_MIN_BRANCH_COVERAGE:-45}"

cleanup() {
  rm -f "$coverage_file"
}
trap cleanup EXIT

cd "$root_dir"
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj \
  --configuration Release \
  --no-build \
  --no-restore \
  --filter-not-trait "Category=Phase1Acceptance" \
  --filter-not-trait "Category=Phase3ExternalAcceptance" \
  --max-parallel-test-modules 1 \
  --coverage \
  --coverage-output "$coverage_file" \
  --coverage-output-format cobertura

read -r line_rate branch_rate < <(
  sed -n 's/.*<coverage line-rate="\([0-9.]*\)" branch-rate="\([0-9.]*\)".*/\1 \2/p' "$coverage_file" | head -n 1
)

if [[ -z "${line_rate:-}" || -z "${branch_rate:-}" ]]; then
  echo "Coverage gate failed: Cobertura summary could not be read." >&2
  exit 1
fi

line_percent="$(awk -v rate="$line_rate" 'BEGIN { printf "%.2f", rate * 100 }')"
branch_percent="$(awk -v rate="$branch_rate" 'BEGIN { printf "%.2f", rate * 100 }')"

if ! awk -v actual="$line_percent" -v minimum="$minimum_line" 'BEGIN { exit !(actual >= minimum) }'; then
  echo "Coverage gate failed: line coverage ${line_percent}% is below ${minimum_line}%." >&2
  exit 1
fi

if ! awk -v actual="$branch_percent" -v minimum="$minimum_branch" 'BEGIN { exit !(actual >= minimum) }'; then
  echo "Coverage gate failed: branch coverage ${branch_percent}% is below ${minimum_branch}%." >&2
  exit 1
fi

echo "Coverage gate passed: line=${line_percent}% (minimum ${minimum_line}%), branch=${branch_percent}% (minimum ${minimum_branch}%)."
