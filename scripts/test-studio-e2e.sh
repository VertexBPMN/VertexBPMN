#!/usr/bin/env bash
# Linux equivalent of scripts/test-studio-e2e.ps1
#
# Runs the local Studio E2E suite against a real API + Studio + browser with
# PostgreSQL and RabbitMQ. On Linux the WSL-Containers infrastructure is replaced
# by Docker (see scripts/docker-infra.sh); 'Existing' reuses already-running
# services. All arguments mirror the PowerShell version (keys matched
# case-insensitively, "-Key value").
#
# Usage:
#   ./test-studio-e2e.sh [-Infrastructure Auto|Docker|Existing] \
#       [-PostgresHost 127.0.0.1] [-PostgresPort 55432] \
#       [-RabbitMqHost 127.0.0.1] [-RabbitMqPort 55672] \
#       [-User vertexbpmn] [-Password vertexbpmn-local] [-TestMethod <name>]
set -euo pipefail

# --- defaults (mirror test-studio-e2e.ps1) ---
INFRASTRUCTURE="Auto"                # Auto | Docker | Existing
POSTGRES_HOST="127.0.0.1"
POSTGRES_PORT="55432"
RABBITMQ_HOST="127.0.0.1"
RABBITMQ_PORT="55672"
RABBITMQ_MGMT_PORT="15673"
USER="vertexbpmn"
PASSWORD="${VERTEXBPMN_LINUX_PASSWORD:-vertexbpmn-local}"
TEST_METHOD=""

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
api_project="$repo_root/src/VertexBPMN.Api/VertexBPMN.Api.csproj"
test_project="$repo_root/tests/VertexBPMN.Studio.UiTests/VertexBPMN.Studio.UiTests.csproj"
docker_script="$repo_root/scripts/docker-infra.sh"

while [[ $# -gt 0 ]]; do
    case "${1,,}" in
        -infrastructure) INFRASTRUCTURE="${2:?missing value}"; shift 2 ;;
        -postgreshost) POSTGRES_HOST="${2:?missing value}"; shift 2 ;;
        -postgresport) POSTGRES_PORT="${2:?missing value}"; shift 2 ;;
        -rabbitmqhost) RABBITMQ_HOST="${2:?missing value}"; shift 2 ;;
        -rabbitmqport) RABBITMQ_PORT="${2:?missing value}"; shift 2 ;;
        -rabbitmqmanagementport) RABBITMQ_MGMT_PORT="${2:?missing value}"; shift 2 ;;
        -user) USER="${2:?missing value}"; shift 2 ;;
        -password) PASSWORD="${2:?missing value}"; shift 2 ;;
        -testmethod) TEST_METHOD="${2:?missing value}"; shift 2 ;;
        -h|-help|--help)
            sed -n '2,20p' "${BASH_SOURCE[0]}"
            exit 0
            ;;
        *) echo "ERROR: unknown argument: $1" >&2; exit 2 ;;
    esac
done

# --- infrastructure mode resolution (Auto -> Docker when available, else Existing) ---
case "${INFRASTRUCTURE,,}" in
    auto)
        if command -v docker >/dev/null 2>&1; then
            INFRASTRUCTURE="Docker"
        else
            INFRASTRUCTURE="Existing"
        fi
        ;;
    docker|existing) INFRASTRUCTURE="${INFRASTRUCTURE,,}" ;;
    *) echo "ERROR: -Infrastructure must be Auto, Docker or Existing." >&2; exit 2 ;;
esac

run_id="$(cat /proc/sys/kernel/random/uuid 2>/dev/null | tr -d '-' || uuidgen | tr -d '-')"
artifacts_directory="$repo_root/tests/VertexBPMN.Studio.UiTests/TestResults/studio-e2e/$run_id"

# --- build the real API, Studio and local browser test host ---
echo "Building the real API, Studio and local browser test host before infrastructure startup..."
dotnet build "$api_project" --configuration Release --nologo --disable-build-servers --maxcpucount:1
dotnet build "$test_project" --configuration Release --nologo --disable-build-servers --maxcpucount:1

# --- infrastructure ---
if [[ "$INFRASTRUCTURE" == "Docker" ]]; then
    echo "Ensuring local PostgreSQL and RabbitMQ via Docker..."
    "$docker_script" start \
        --user "$USER" --password "$PASSWORD" \
        --postgres-port "$POSTGRES_PORT" \
        --rabbitmq-port "$RABBITMQ_PORT" \
        --rabbitmq-management-port "$RABBITMQ_MGMT_PORT"
else
    echo "Using existing local PostgreSQL and RabbitMQ installations."
fi

# --- reachability probe ---
check_tcp() { # host port description
    if (exec 3<>"/dev/tcp/$1/$2") 2>/dev/null; then
        exec 3>&- 3<&- || true
        return 0
    fi
    echo "ERROR: $3 is not reachable at $1:$2." >&2
    return 1
}
check_tcp "$POSTGRES_HOST" "$POSTGRES_PORT" "PostgreSQL"
check_tcp "$RABBITMQ_HOST" "$RABBITMQ_PORT" "RabbitMQ"

postgres_base="Host=$POSTGRES_HOST;Port=$POSTGRES_PORT;Username=$USER;Password=$PASSWORD;SSL Mode=Disable;Timeout=10;Command Timeout=30"

mkdir -p "$artifacts_directory"
report="$artifacts_directory/results.html"
xml_report="$artifacts_directory/results.xml"

runner="$repo_root/tests/VertexBPMN.Studio.UiTests/bin/Release/net10.0/VertexBPMN.Studio.UiTests"
if [[ ! -x "$runner" ]]; then
    echo "ERROR: the local xUnit runner was not produced at '$runner'." >&2
    exit 1
fi

runner_args=(
    -trait "Category=LocalStudioE2E"
    -parallelMode none
    -longRunning 30
    -showLiveOutput
    -result-html "$report"
    -result-xml "$xml_report"
)
if [[ -n "$TEST_METHOD" ]]; then
    if [[ "$TEST_METHOD" == *.* ]]; then
        method_filter="$TEST_METHOD"
    else
        method_filter="VertexBPMN.Studio.UiTests.LocalStudioInfrastructureTests.$TEST_METHOD"
    fi
    runner_args+=(-method "$method_filter")
fi

echo "Running local real Studio E2E infrastructure tests (run $run_id)..."
# The AMQP URI uses the real password (unlike the PowerShell variant's literal '***',
# which makes RabbitMQ auth fail). $PASSWORD defaults to 'vertexbpmn-local'.
env \
    VERTEXBPMN_STUDIO_E2E_ENABLED=true \
    VERTEXBPMN_STUDIO_E2E_RUN_ID="$run_id" \
    VERTEXBPMN_STUDIO_E2E_ARTIFACTS="$artifacts_directory" \
    VERTEXBPMN_E2E_BPMN_CONNECTION="$postgres_base;Database=vertexbpmn_bpmn" \
    VERTEXBPMN_E2E_TENANT_CONNECTION="$postgres_base;Database=vertexbpmn_tenants" \
    VERTEXBPMN_E2E_SIMULATION_CONNECTION="$postgres_base;Database=vertexbpmn_simulation" \
    VERTEXBPMN_E2E_EVENTS_CONNECTION="$postgres_base;Database=vertexbpmn_events" \
    VERTEXBPMN_E2E_DECISION_CONNECTION="$postgres_base;Database=vertexbpmn_decision" \
    VERTEXBPMN_E2E_RABBITMQ_CONNECTION="amqp://$USER:$PASSWORD@$RABBITMQ_HOST:$RABBITMQ_PORT/" \
    "$runner" "${runner_args[@]}"

if [[ ! -f "$xml_report" ]]; then
    echo "ERROR: the local Studio E2E runner did not produce the expected XML report: $xml_report" >&2
    exit 1
fi

total="$(python3 -c "
import re,sys
data=open('$xml_report',encoding='utf-8').read()
print(sum(int(t) for t in re.findall(r'total=\"(\d+)\"', data)))
")"
if [[ "$total" == "0" ]]; then
    echo "ERROR: the local Studio E2E filter discovered zero tests. Method filter: '$TEST_METHOD'. Report: $report" >&2
    exit 1
fi

echo "E2E run finished: $total test(s) ran. Report: $report"
