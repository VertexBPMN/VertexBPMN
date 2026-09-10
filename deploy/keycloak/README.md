# Keycloak test realm

This directory contains the version-controlled, secret-free Keycloak realm used by the local `OidcTest` profile.

- Keycloak image: `quay.io/keycloak/keycloak:26.7.3`
- Realm: `vertexbpmn`
- API audience/client: `vertexbpmn-api`
- Studio client: `vertexbpmn-studio`
- Claims scope: `vertexbpmn-claims`

The realm intentionally contains no users, passwords, client secret, bootstrap administrator, or environment-specific host name. Keycloak generates a secret for the confidential Studio client when the realm is imported. Supply that value to Studio through `StudioAuthentication__ClientSecret`; never add it to this file or another tracked configuration file. The separate user-profile template declares `tenant_id` as a managed, single-valued attribute that users can view but only administrators can edit.

The checked-in loopback redirect URI is reserved for local testing. Production deployments must use an environment-specific realm configuration with exact HTTPS redirect and post-logout URIs.

## Local WSLC test instance

Set four process-scoped environment variables to non-default local test values:

```powershell
$env:VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD = '<random local admin password>'
$env:VERTEXBPMN_KEYCLOAK_DB_PASSWORD = '<random local database password>'
$env:VERTEXBPMN_KEYCLOAK_STUDIO_CLIENT_SECRET = '<random local confidential-client secret>'
$env:VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD = '<random local test-user password>'
```

Then use the dedicated lifecycle script:

```powershell
./scripts/keycloak-oidc-test.ps1 -Action Start
./scripts/keycloak-oidc-test.ps1 -Action Status
./scripts/keycloak-oidc-test.ps1 -Action Stop
./scripts/keycloak-oidc-test.ps1 -Action Remove
```

The script owns only the exact dedicated resources `vertexbpmn-keycloak-test`, `vertexbpmn-keycloak-postgres-test`, `vertexbpmn-keycloak-postgres-test-data`, and `vertexbpmn-keycloak-test`. `Stop` preserves their state; the explicit `Remove` action deletes only those test resources, including their test database. Re-running `Start` updates the dedicated test users and Studio secret without recreating the realm. Persisted resources must be restarted with the same bootstrap administrator and database passwords that created them. The MFA test user receives the `CONFIGURE_TOTP` required action and must enroll its authenticator before the MFA browser scenario can pass.

On affected WSLC versions a freshly created container can report Keycloak as listening before its host port is actually bound. The readiness probe performs at most one restart of the dedicated Keycloak container after that condition is observed; it never restarts PostgreSQL, RabbitMQ, or operator-managed containers.

The values are passed transiently to local WSLC/container processes. Do not run the script on a shared or untrusted host, and never paste the values into logs, issue reports, or tracked files. This `start-dev` topology is a local acceptance fixture, not a production Keycloak deployment.

To start the existing ExternalServices AppHost together with this real OIDC profile, set the same four variables and run:

```powershell
./scripts/wslc-apphost.ps1 -Action Start -OidcTest
```

Without `-OidcTest`, every existing AppHost/WSLC behavior remains unchanged. `ExistingInfrastructure` can also be combined with `OidcTest`; in that case PostgreSQL and RabbitMQ remain operator-managed while only the dedicated Keycloak test resources are managed by the Keycloak helper.
