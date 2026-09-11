#!/usr/bin/env bash
# One-shot Keycloak realm bootstrap for the compose stack.
# Runs inside the quay.io/keycloak/keycloak image (bash + kcadm.sh; NO python/jq,
# so JSON ids are extracted with sed). Reaches Keycloak admin on the compose
# network (KCADM_SERVER=http://keycloak:8080). Idempotent: safe to re-run.
set -euo pipefail

KC="${KCADM_SERVER:-http://keycloak:8080}"
KCADM=/opt/keycloak/bin/kcadm.sh

admin_user="${KEYCLOAK_ADMIN_USER:-vertexbpmn-admin}"
admin_pw="${KEYCLOAK_ADMIN_PASSWORD:?KEYCLOAK_ADMIN_PASSWORD required}"
studio_secret="${KEYCLOAK_STUDIO_CLIENT_SECRET:?KEYCLOAK_STUDIO_CLIENT_SECRET required}"
test_user="${KEYCLOAK_TEST_USER:-vertexbpmn-user}"
test_pw="${KEYCLOAK_TEST_USER_PASSWORD:?KEYCLOAK_TEST_USER_PASSWORD required}"
test_tenant="${KEYCLOAK_TEST_USER_TENANT:-tenant-a}"
test_role="${KEYCLOAK_TEST_USER_ROLE:-ProcessManager}"

# Extract the top-level "id" (UUID) of the single object returned by a
# `kcadm get <resource> -q <key>=<value>` query. The top-level id prints before
# any nested "id", so head -1 is reliable.
json_id_for() { # $1=resource  $2=key  $3=value
    "$KCADM" get "$1" -r vertexbpmn -q "$2=$3" 2>/dev/null \
      | sed -nE 's/^[[:space:]]*"id"[[:space:]]*:[[:space:]]*"([^"]+)".*$/\1/p' \
      | head -1 || true
}

echo "Connecting to Keycloak admin at $KC ..."
"$KCADM" config credentials --server "$KC" --realm master \
    --user "$admin_user" --password "$admin_pw" >/dev/null

echo "Set studio client secret ..."
studio_id="$(json_id_for clients clientId vertexbpmn-studio)"
[ -n "$studio_id" ] || { echo "ERROR: studio client vertexbpmn-studio not found" >&2; exit 2; }
"$KCADM" update "clients/$studio_id" -r vertexbpmn -s "secret=$studio_secret" >/dev/null

echo "Ensuring test user '$test_user' ..."
uid="$(json_id_for users username "$test_user")"
if [ -z "$uid" ]; then
    "$KCADM" create users -r vertexbpmn \
        -s "username=$test_user" -s "email=$test_user@vertexbpmn.test" \
        -s "firstName=VertexBPMN" -s "lastName=Test User" \
        -s "enabled=true" -s "emailVerified=true" >/dev/null
    uid="$(json_id_for users username "$test_user")"
fi
[ -n "$uid" ] || { echo "ERROR: could not resolve user $test_user" >&2; exit 2; }
"$KCADM" update "users/$uid" -r vertexbpmn \
    -s "attributes.tenant_id=[\"$test_tenant\"]" >/dev/null
"$KCADM" set-password -r vertexbpmn --username "$test_user" \
    --new-password "$test_pw" >/dev/null
"$KCADM" add-roles -r vertexbpmn --uusername "$test_user" \
    --cclientid vertexbpmn-api --rolename "$test_role" >/dev/null

echo "Keycloak bootstrap complete."
