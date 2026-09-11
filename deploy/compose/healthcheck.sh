#!/usr/bin/env bash
# Readiness probe for containers that ship no curl/wget (the .NET aspnet images).
# Uses bash /dev/tcp to send a raw HTTP/1.0 GET and require a 2xx/redirect.
# A 302 (OIDC auth challenge) counts as healthy for auth-protected apps.
# The Host header MUST carry the port, or host-only OIDC redirect_uri derivation
# produces an unregistered URI (e.g. http://localhost/... ) and Keycloak returns
# invalid_request -> HTTP 500.
# Usage: hcheck <port> <path>   e.g.  hcheck 51870 /api/ready
set -u
port="${1:?port required}"
path="${2:?path required}"
exec 3<>"/dev/tcp/127.0.0.1/$port" || exit 1
printf 'GET %s HTTP/1.0\r\nHost: localhost:%s\r\n\r\n' "$path" "$port" >&3
grep -qE '200 OK|302 Found' <&3
