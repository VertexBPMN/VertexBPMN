#!/usr/bin/env bash
# Linux/Docker equivalent of scripts/wslc-apphost.ps1 (InfrastructureOnly role).
#
# Manages the PostgreSQL + RabbitMQ containers used by the local Studio E2E suite.
# Docker replaces the WSL-Containers used by the PowerShell variant; container
# names, volumes, ports and database names mirror wslc-apphost.ps1.
#
# Usage:
#   ./docker-infra.sh start [--user vertexbpmn] [--password vertexbpmn-local] \
#       [--postgres-port 55432] [--rabbitmq-port 55672] [--rabbitmq-management-port 15673]
#   ./docker-infra.sh stop
#   ./docker-infra.sh status
set -euo pipefail

ACTION="start"
POSTGRES_IMAGE="postgres:17-alpine"
RABBITMQ_IMAGE="rabbitmq:4-management-alpine"
USER="vertexbpmn"
PASSWORD="${VERTEXBPMN_LINUX_PASSWORD:-vertexbpmn-local}"
POSTGRES_PORT="55432"
RABBITMQ_PORT="55672"
RABBITMQ_MGMT_PORT="15673"

while [[ $# -gt 0 ]]; do
    case "${1,,}" in
        start|stop|status) ACTION="${1,,}"; shift ;;
        --postgres-image) POSTGRES_IMAGE="${2:?missing value}"; shift 2 ;;
        --rabbitmq-image) RABBITMQ_IMAGE="${2:?missing value}"; shift 2 ;;
        --user) USER="${2:?missing value}"; shift 2 ;;
        --password) PASSWORD="${2:?missing value}"; shift 2 ;;
        --postgres-port) POSTGRES_PORT="${2:?missing value}"; shift 2 ;;
        --rabbitmq-port) RABBITMQ_PORT="${2:?missing value}"; shift 2 ;;
        --rabbitmq-management-port) RABBITMQ_MGMT_PORT="${2:?missing value}"; shift 2 ;;
        *) echo "ERROR: unknown argument: $1" >&2; exit 2 ;;
    esac
done

NETWORK="vertexbpmn"
POSTGRES_CONTAINER="vertexbpmn-postgres"
RABBITMQ_CONTAINER="vertexbpmn-rabbitmq"
POSTGRES_VOLUME="vertexbpmn-postgres-data"
RABBITMQ_VOLUME="vertexbpmn-rabbitmq-data"
DATABASES=(vertexbpmn_bpmn vertexbpmn_tenants vertexbpmn_simulation vertexbpmn_events vertexbpmn_decision)

need_docker() {
    command -v docker >/dev/null 2>&1 || { echo "ERROR: docker is required but is not available on PATH." >&2; exit 1; }
}

container_exists() { docker container inspect "$1" >/dev/null 2>&1; }
container_running() {
    local running
    running="$(docker inspect -f '{{.State.Running}}' "$1" 2>/dev/null || true)"
    [[ "$running" == "true" ]]
}

ensure_network() {
    if ! docker network inspect "$NETWORK" >/dev/null 2>&1; then
        echo "Creating network '$NETWORK'..."
        docker network create "$NETWORK" >/dev/null
    fi
}

ensure_volume() {
    if ! docker volume inspect "$1" >/dev/null 2>&1; then
        echo "Creating volume '$1'..."
        docker volume create "$1" >/dev/null
    fi
}

# ensure_container <name> <docker run args...>
ensure_container() {
    local name="$1"
    shift
    if container_exists "$name"; then
        if ! container_running "$name"; then
            echo "Starting existing container '$name'..."
            docker start "$name" >/dev/null
        fi
        return
    fi
    echo "Creating container '$name'..."
    docker run --detach --name "$name" "$@" >/dev/null
}

# wait_ready <container> <seconds> <probe...>
wait_ready() {
    local name="$1" secs="$2"
    shift 2
    local deadline=$(( $(date +%s) + secs ))
    until docker exec "$name" "$@" >/dev/null 2>&1; do
        if [[ $(date +%s) -ge $deadline ]]; then
            echo "ERROR: container '$name' did not become ready within ${secs}s." >&2
            docker logs --tail 40 "$name" >&2 || true
            return 1
        fi
        sleep 2
    done
}

start_infra() {
    need_docker
    ensure_network
    ensure_volume "$POSTGRES_VOLUME"
    ensure_volume "$RABBITMQ_VOLUME"

    ensure_container "$POSTGRES_CONTAINER" \
        --network "$NETWORK" \
        --publish "127.0.0.1:$POSTGRES_PORT:5432" \
        --env "POSTGRES_USER=$USER" \
        --env "POSTGRES_PASSWORD=$PASSWORD" \
        --env "POSTGRES_DB=postgres" \
        --volume "$POSTGRES_VOLUME:/var/lib/postgresql/data" \
        "$POSTGRES_IMAGE"

    ensure_container "$RABBITMQ_CONTAINER" \
        --network "$NETWORK" \
        --publish "127.0.0.1:$RABBITMQ_PORT:5672" \
        --publish "127.0.0.1:$RABBITMQ_MGMT_PORT:15672" \
        --env "RABBITMQ_DEFAULT_USER=$USER" \
        --env "RABBITMQ_DEFAULT_PASS=$PASSWORD" \
        --volume "$RABBITMQ_VOLUME:/var/lib/rabbitmq" \
        "$RABBITMQ_IMAGE"

    echo "Waiting for PostgreSQL and RabbitMQ..."
    wait_ready "$POSTGRES_CONTAINER" 90 pg_isready -U "$USER" -d postgres
    wait_ready "$RABBITMQ_CONTAINER" 90 rabbitmq-diagnostics -q ping

    for db in "${DATABASES[@]}"; do
        local exists
        exists="$(docker exec "$POSTGRES_CONTAINER" psql -U "$USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$db'")"
        if [[ "$exists" != "1" ]]; then
            echo "Creating PostgreSQL database '$db'..."
            docker exec "$POSTGRES_CONTAINER" createdb -U "$USER" "$db"
        fi
    done

    status_infra
}

stop_infra() {
    need_docker
    for name in "$RABBITMQ_CONTAINER" "$POSTGRES_CONTAINER"; do
        if container_exists "$name" && container_running "$name"; then
            echo "Stopping container '$name'..."
            docker stop "$name" >/dev/null
        fi
    done
}

status_infra() {
    for name in "$POSTGRES_CONTAINER" "$RABBITMQ_CONTAINER"; do
        local st
        if container_running "$name"; then
            st="running"
        elif container_exists "$name"; then
            st="stopped"
        else
            st="missing"
        fi
        echo "$name: $st"
    done
}

case "$ACTION" in
    start) start_infra ;;
    stop) stop_infra ;;
    status) status_infra ;;
esac
