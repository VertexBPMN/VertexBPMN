#!/usr/bin/env bash
set -euo pipefail

mode="${1:-verify}"
if [[ "$mode" != "verify" && "$mode" != "update" ]]; then
  echo "Usage: $0 [verify|update]" >&2
  exit 2
fi

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
snapshot="$root_dir/src/VertexBPMN.Api/Contracts/openapi.json"
generated="$(mktemp)"
server_log="$(mktemp)"
database_dir="$(mktemp -d)"
port="${VERTEXBPMN_OPENAPI_PORT:-5088}"
configuration="${VERTEXBPMN_OPENAPI_CONFIGURATION:-Release}"

cleanup() {
  if [[ -n "${server_pid:-}" ]] && kill -0 "$server_pid" 2>/dev/null; then
    kill "$server_pid" 2>/dev/null || true
    wait "$server_pid" 2>/dev/null || true
  fi
  rm -f "$generated" "$server_log"
  rm -rf "$database_dir"
}
trap cleanup EXIT

cd "$root_dir"
ASPNETCORE_ENVIRONMENT=Test \
DOTNET_ENVIRONMENT=Test \
OperationalMode=Test \
Database__ApplyMigrationsOnStartup=true \
ConnectionStrings__BpmnDbContext="Data Source=${database_dir}/bpmn.db" \
ConnectionStrings__TenantDbContext="Data Source=${database_dir}/tenants.db" \
ConnectionStrings__SimulationScenarioDbContext="Data Source=${database_dir}/simulation.db" \
ConnectionStrings__ProcessMiningEvents="Data Source=${database_dir}/mining.db" \
ConnectionStrings__DecisionDbContext="Data Source=${database_dir}/decision.db" \
ConnectionStrings__DependencyRegistry="Data Source=${database_dir}/dependencies.db" \
ASPNETCORE_URLS="http://127.0.0.1:${port}" \
dotnet run --project src/VertexBPMN.Api/VertexBPMN.Api.csproj --configuration "$configuration" --no-build --no-launch-profile >"$server_log" 2>&1 &
server_pid=$!

for _ in $(seq 1 60); do
  if curl --fail --silent "http://127.0.0.1:${port}/api/swagger/v1/swagger.json" >"$generated"; then
    break
  fi
  if ! kill -0 "$server_pid" 2>/dev/null; then
    cat "$server_log" >&2
    exit 1
  fi
  sleep 1
done

if [[ ! -s "$generated" ]]; then
  echo "Timed out waiting for the OpenAPI document." >&2
  cat "$server_log" >&2
  exit 1
fi

jq -e '.openapi and .paths and .components.schemas' "$generated" >/dev/null

if [[ "$mode" == "update" ]]; then
  # The tracked contract uses CRLF; preserve it so updating on Linux does not
  # turn a small API change into a whole-file line-ending diff.
  jq -S . "$generated" | sed 's/$/\r/' >"$snapshot"
  echo "Updated OpenAPI snapshot: $snapshot"
  exit 0
fi

diff -u <(jq -S . "$snapshot") <(jq -S . "$generated")
echo "OpenAPI snapshot matches the generated document."
