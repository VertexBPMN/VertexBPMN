#!/usr/bin/env bash
#
# Linux/docker port of scripts/keycloak-oidc-e2e.ps1 + keycloak-oidc-test.ps1.
# Starts a dedicated Keycloak (docker) + real API + real Studio, then runs the
# browser login / API authorization / session-refresh / logout acceptance test.
#
# Required env vars (process secrets):
#   VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD
#   VERTEXBPMN_KEYCLOAK_DB_PASSWORD
#   VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET
#   VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD
#
# Usage: scripts/keycloak-oidc-e2e.sh [--keep-keycloak]
set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
KEYCLOAK_SCRIPT="$REPOSITORY_ROOT/scripts/keycloak-oidc-test.sh"

KEEP_KEYCLOAK=0
if [[ "${1:-}" == "--keep-keycloak" ]]; then KEEP_KEYCLOAK=1; fi

ACCESS_TOKEN_LIFESPAN="${ACCESS_TOKEN_LIFESPAN:-70}"
API_PORT=51870
STUDIO_PORT=5263
KEYCLOAK_PORT=58080

API_ASSEMBLY="$REPOSITORY_ROOT/src/VertexBPMN.Api/bin/Release/net10.0/VertexBPMN.Api.dll"
STUDIO_ASSEMBLY="$REPOSITORY_ROOT/src/VertexBPMN.Studio/bin/Release/net10.0/VertexBPMN.Studio.dll"
UI_TEST_ASSEMBLY="$REPOSITORY_ROOT/tests/VertexBPMN.Studio.UiTests/bin/Release/net10.0/VertexBPMN.Studio.UiTests.dll"
RESULTS_DIR="$REPOSITORY_ROOT/tests/VertexBPMN.Studio.UiTests/TestResults/keycloak-oidc"

export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
export DOTNET_CLI_HOME="$HOME/.dotnet"
export NUGET_PACKAGES="$HOME/.dotnet/.nuget/packages"

require_secrets() {
    local missing=()
    for var in \
        VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD \
        VERTEXBPMN_KEYCLOAK_DB_PASSWORD \
        VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET \
        VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD; do
        if [[ -z "${!var:-}" ]]; then missing+=("$var"); fi
    done
    if [[ ${#missing[@]} -gt 0 ]]; then
        echo "Set the following process secrets first: ${missing[*]}" >&2
        exit 1
    fi
}

# ---------------------------------------------------------------- Keycloak
NETWORK=vertexbpmn-keycloak-test
DB_CONTAINER=vertexbpmn-keycloak-postgres-test
KC_CONTAINER=vertexbpmn-keycloak-test
DB_VOLUME=vertexbpmn-keycloak-postgres-test-data
KC_IMAGE=quay.io/keycloak/keycloak:26.7.3
PG_IMAGE=postgres:17-alpine

container_running() { [[ "$(docker inspect -f '{{.State.Running}}' "$1" 2>/dev/null)" == "true" ]]; }
container_exists()  { docker inspect "$1" >/dev/null 2>&1; }

kcadm() { docker exec "$KC_CONTAINER" /opt/keycloak/bin/kcadm.sh "$@"; }

# Parse a JSON array field by key from `kcadm get` output (kcadm's -q returns JSON).
json_id_for() { # $1=query resource  $2=key  $3=value
    local out; out="$(kcadm get "$1" -r vertexbpmn -q "$2=$3")"
    python3 -c 'import sys,json
arr=json.load(sys.stdin)
m=[x for x in arr if x.get(sys.argv[1])==sys.argv[2]]
assert len(m)==1, m
print(m[0]["id"])' "$2" "$3" <<<"$out"
}

ensure_user() { # $1=username $2=tenantId $3=role $4=mfa(0/1)
    local username="$1" tenant="$2" role="$3" mfa="$4"
    local count; count="$(kcadm get users -r vertexbpmn -q "username=$username" | python3 -c 'import sys,json;print(len(json.load(sys.stdin)))')"
    if [[ "$count" == "0" ]]; then
        kcadm create users -r vertexbpmn \
            -s "username=$username" -s "email=$username@vertexbpmn.test" \
            -s "firstName=VertexBPMN" -s "lastName=Test User" \
            -s "enabled=true" -s "emailVerified=true" >/dev/null
    elif [[ "$count" != "1" ]]; then
        echo "More than one Keycloak user matched '$username'" >&2; exit 1
    fi
    local uid; uid="$(json_id_for users username "$username")"
    local req_actions='[]'; [[ "$mfa" == "1" ]] && req_actions='["CONFIGURE_TOTP"]'
    kcadm update "users/$uid" -r vertexbpmn \
        -s "email=$username@vertexbpmn.test" -s "firstName=VertexBPMN" -s "lastName=Test User" \
        -s "emailVerified=true" -s "attributes.tenant_id=[\"$tenant\"]" \
        -s "requiredActions=$req_actions" >/dev/null
    kcadm set-password -r vertexbpmn --username "$username" \
        --new-password "$VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD" >/dev/null
    kcadm add-roles -r vertexbpmn --uusername "$username" \
        --cclientid vertexbpmn-api --rolename "$role" >/dev/null
    echo "user ready: $username"
}

bootstrap_realm() {
    # kcadm config persists its auth token inside the container; re-authenticate each run.
    kcadm config credentials --server http://localhost:8080 --realm master \
        --user vertexbpmn-admin --password "$VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD" >/dev/null
    kcadm update "realms/vertexbpmn" -s "accessTokenLifespan=$ACCESS_TOKEN_LIFESPAN" >/dev/null
    local studio_id
    studio_id="$(json_id_for clients clientId vertexbpmn-studio)"
    kcadm update "clients/$studio_id" -r vertexbpmn -s "secret=$VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET" >/dev/null
    ensure_user vertexbpmn-user    tenant-a ProcessManager 0
    ensure_user vertexbpmn-mfa-user tenant-b ReadOnly       1
}

docker_start_keycloak() {
    require_secrets
    docker network create "$NETWORK" 2>/dev/null || true
    docker volume create "$DB_VOLUME" >/dev/null 2>&1 || true

    if ! container_exists "$DB_CONTAINER"; then
        docker run -d --name "$DB_CONTAINER" --network "$NETWORK" \
            -e POSTGRES_USER=keycloak -e POSTGRES_PASSWORD="$VERTEXBPMN_KEYCLOAK_DB_PASSWORD" \
            -e POSTGRES_DB=keycloak -v "$DB_VOLUME:/var/lib/postgresql/data" \
            "$PG_IMAGE" >/dev/null
    fi
    if ! container_running "$DB_CONTAINER"; then
        docker start "$DB_CONTAINER" >/dev/null
    fi
    echo -n "waiting for keycloak postgres..."
    local deadline=$(( $(date +%s) + 120 ))
    until docker exec "$DB_CONTAINER" pg_isready -U keycloak -d keycloak >/dev/null 2>&1; do
        [[ $(date +%s) -lt $deadline ]] || { echo " DB not ready"; exit 1; }
        echo -n "."; sleep 2
    done; echo " ready"

    if ! container_exists "$KC_CONTAINER"; then
        docker run -d --name "$KC_CONTAINER" --network "$NETWORK" -p "${KEYCLOAK_PORT}:8080" \
            -e KC_BOOTSTRAP_ADMIN_USERNAME=vertexbpmn-admin \
            -e KC_BOOTSTRAP_ADMIN_PASSWORD="$VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD" \
            -e KC_DB=postgres -e KC_DB_URL_HOST="$DB_CONTAINER" \
            -e KC_DB_URL_DATABASE=keycloak -e KC_DB_USERNAME=keycloak \
            -e KC_DB_PASSWORD="$VERTEXBPMN_KEYCLOAK_DB_PASSWORD" \
            -e KC_HOSTNAME="http://localhost:$KEYCLOAK_PORT" \
            -v "$REPOSITORY_ROOT/deploy/keycloak/vertexbpmn-realm.json:/opt/keycloak/data/import/vertexbpmn-realm.json:ro" \
            "$KC_IMAGE" start-dev --import-realm --http-port=8080 --health-enabled=true >/dev/null
    fi
    if ! container_running "$KC_CONTAINER"; then
        docker start "$KC_CONTAINER" >/dev/null
    fi

    local disc="http://localhost:$KEYCLOAK_PORT/realms/vertexbpmn/.well-known/openid-configuration"
    echo -n "waiting for keycloak discovery..."
    local deadline=$(( $(date +%s) + 180 ))
    until curl -sf --max-time 5 "$disc" | grep -q '"issuer"'; do
        [[ $(date +%s) -lt $deadline ]] || { echo " Keycloak not ready"; docker logs --tail 60 "$KC_CONTAINER"; exit 1; }
        echo -n "."; sleep 2
    done; echo " ready"

    bootstrap_realm
    echo "OIDC test realm ready at http://localhost:$KEYCLOAK_PORT/realms/vertexbpmn"
}

docker_remove_keycloak() {
    for c in "$KC_CONTAINER" "$DB_CONTAINER"; do
        container_exists "$c" && { docker rm -f "$c" >/dev/null 2>&1 || true; }
    done
    docker volume rm -f "$DB_VOLUME" >/dev/null 2>&1 || true
    docker network rm "$NETWORK" >/dev/null 2>&1 || true
}

docker_status_keycloak() {
    for c in "$DB_CONTAINER" "$KC_CONTAINER"; do
        if container_running "$c"; then echo "$c: running"
        elif container_exists "$c"; then echo "$c: stopped"
        else echo "$c: missing"; fi
    done
}

# ------------------------------------------------------------------- e2e
build_all() {
    echo "== Building API, Studio and UiTests (Release) =="
    dotnet build "$REPOSITORY_ROOT/src/VertexBPMN.Api/VertexBPMN.Api.csproj" -c Release --no-restore --nologo -v q
    dotnet build "$REPOSITORY_ROOT/tests/VertexBPMN.Studio.UiTests/VertexBPMN.Studio.UiTests.csproj" \
        -c Release --no-restore --nologo -v q -p:SkipBpmnIoAssetBuild=true
}

wait_endpoint() { # $1=url $2=name $3=pid
    local deadline=$(( $(date +%s) + 180 ))
    until curl -sf -o /dev/null "$1" 2>/dev/null; do
        if ! kill -0 "$3" 2>/dev/null; then echo "$2 process exited before readiness." >&2; exit 1; fi
        [[ $(date +%s) -lt $deadline ]] || { echo "$2 did not become ready at $1" >&2; exit 1; }
        sleep 1
    done
}

run_e2e() {
    require_secrets
    build_all
    mkdir -p "$RESULTS_DIR"
    local api_out="$RESULTS_DIR/api.stdout.log" api_err="$RESULTS_DIR/api.stderr.log"
    local st_out="$RESULTS_DIR/studio.stdout.log" st_err="$RESULTS_DIR/studio.stderr.log"

    docker_start_keycloak

    echo "== Starting real API on :$API_PORT =="
    env ASPNETCORE_ENVIRONMENT=OidcTest DOTNET_ENVIRONMENT=OidcTest OperationalMode=OidcTest \
        Database__ApplyMigrationsOnStartup=true Operational__Metrics__Enabled=false \
        Runtime__Outbox__Enabled=false Runtime__Outbox__Provider=Disabled \
        ConnectionStrings__DependencyRegistry="Data Source=$RESULTS_DIR/dependencies.db" \
        Jwt__Authority="http://localhost:$KEYCLOAK_PORT/realms/vertexbpmn" \
        Jwt__Issuer="http://localhost:$KEYCLOAK_PORT/realms/vertexbpmn" \
        Jwt__Audience=vertexbpmn-api Jwt__RequireHttpsMetadata=false Jwt__UseDevelopmentApiKey=false \
        dotnet "$API_ASSEMBLY" --urls "http://localhost:$API_PORT" \
        >"$api_out" 2>"$api_err" &
    API_PID=$!
    wait_endpoint "http://localhost:$API_PORT/api/ready" "VertexBPMN API" "$API_PID"

    echo "== Starting real Studio on :$STUDIO_PORT =="
    # ApiBaseUrl is read from appsettings.OidcTest.json (127.0.0.1:51870) but override explicitly.
    env ASPNETCORE_ENVIRONMENT=OidcTest DOTNET_ENVIRONMENT=OidcTest OperationalMode=OidcTest \
        ApiBaseUrl="http://localhost:$API_PORT/" StudioHttpsRedirection__Enabled=false \
        StudioAuthentication__Authority="http://localhost:$KEYCLOAK_PORT/realms/vertexbpmn" \
        StudioAuthentication__ClientId=vertexbpmn-studio \
        StudioAuthentication__ClientSecret="$VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET" \
        StudioAuthentication__ClaimsScope=vertexbpmn-claims \
        StudioAuthentication__RequireClientSecret=true \
        StudioAuthentication__RequireHttpsMetadata=false \
        StudioAuthentication__LocalDevelopmentEnabled=false \
        StudioAuthentication__UiTestEnabled=false \
        dotnet "$STUDIO_ASSEMBLY" --urls "http://localhost:$STUDIO_PORT" \
        >"$st_out" 2>"$st_err" &
    STUDIO_PID=$!
    wait_endpoint "http://localhost:$STUDIO_PORT/" "VertexBPMN Studio" "$STUDIO_PID"

    echo "== Running browser login / refresh / logout acceptance test =="
    VERTEXBPMN_OIDC_TEST_STUDIO_URL="http://localhost:$STUDIO_PORT/" \
    VERTEXBPMN_KEYCLOAK_TEST_USER=vertexbpmn-user \
    dotnet "$UI_TEST_ASSEMBLY" -class "*KeycloakOidcLocalAcceptanceTests"
    local rc=$?

    kill "$STUDIO_PID" 2>/dev/null || true
    kill "$API_PID" 2>/dev/null || true
    wait "$STUDIO_PID" 2>/dev/null || true
    wait "$API_PID" 2>/dev/null || true

    if [[ $rc -eq 0 ]]; then
        echo "== Keycloak OIDC acceptance PASSED =="
    else
        echo "== Keycloak OIDC acceptance FAILED (exit $rc) ==" >&2
        echo "--- api.stderr (tail) ---"; tail -n 80 "$api_err" 2>/dev/null || true
        echo "--- studio.stderr (tail) ---"; tail -n 80 "$st_err" 2>/dev/null || true
    fi
    return $rc
}

# ------------------------------------------------------------------- main
case "${1:-run}" in
    run)      if [[ $KEEP_KEYCLOAK -eq 1 ]]; then
                  run_e2e; rc=$?
                  if [[ $rc -ne 0 ]]; then exit $rc; fi
                  docker_status_keycloak
              else
                  run_e2e; rc=$?
                  echo "== Removing Keycloak test infra =="
                  docker_remove_keycloak
                  exit $rc
              fi ;;
    start)    docker_start_keycloak ;;
    status)   docker_status_keycloak ;;
    remove)   docker_remove_keycloak ;;
    *)        echo "usage: $0 [run|start|status|remove] [--keep-keycloak]" >&2; exit 2 ;;
esac
