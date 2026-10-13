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

## Passkeys (local pilot)

The template enables Keycloak's passwordless WebAuthn policy with user verification
and discoverable credentials both `required`, attestation `none`, unrestricted
authenticator attachment, and RP ID `localhost`. Hardware security keys remain
eligible. Enabling passkeys does not force enrollment: the
`webauthn-register-passwordless` required action is enabled but is not a default
action for all users. These field names were checked against Keycloak 26.7.3;
runtime acceptance is recorded separately in the implementation report.
The explicit required-action list preserves all 14 standard actions of that
version, including TOTP enrollment. A partial list would replace the defaults
during import and is therefore not sufficient.

`Start` and `Bootstrap` also apply the policy to an existing realm, because
`--import-realm` does not update an already imported realm. Both paths read the
same secret-free template. Every bootstrap resets the dedicated
`vertexbpmn-passkey-user` (tenant `tenant-a`, role `ReadOnly`), its required actions
and WebAuthn credentials. `-RequirePasskeyEnrollment` assigns registration to
this user only. Its password comes from the existing
`VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD` process variable.

For Studio's SSO page, optionally configure
`StudioAuthentication__AccountManagementUrl` with the provider's account console.
In the local `OidcTest` profile this is
`http://localhost:58080/realms/vertexbpmn/account/`. Other environments require
HTTPS. The setting is provider-neutral and optional; no link appears when empty.
The separate opt-in `StudioAuthentication__PasskeyRegistrationEnabled=true`
exposes **Add a passkey** and the authenticated registration route for Keycloak.
It defaults to false and is unavailable with local development or UI-test auth.
`-PasskeyAcceptance` enables it for the local acceptance host and provisions
separate AIA and tenant-removal users. The existing `vertexbpmn-no-tenant`
security fixture is unchanged. The suite includes T13 registration/login,
T14 PAR registration/cancellation, and T15 rejection of tenant-free tokens after
a real passkey authentication.

Run the opt-in Chromium virtual-authenticator test with the four process secrets
above set:

```powershell
./scripts/keycloak-oidc-e2e.ps1 -PasskeyAcceptance -TestInstance passkeys-pilot
./scripts/keycloak-oidc-e2e.ps1 -PasskeyAcceptance -TestInstance passkeys-pilot -TestMethod '*T13_*'
```

`-SecurityAcceptance` continues to select the existing security suite and excludes
passkey tests unless `-PasskeyAcceptance` is also supplied. `-TestInstance` appends
a validated suffix to all four resource names so a pilot can use a separate
database volume. It can also be inherited through
`VERTEXBPMN_KEYCLOAK_TEST_INSTANCE`, including by readiness subprocesses.
The default names are unchanged when no suffix is configured. Each instance
still uses the default acceptance ports, so these runs must be sequential.
For a kept instance use the same suffix for `Status`, `Stop`, `Bootstrap` and
`Remove`; `Remove` deletes only that instance's resources.

Production RP ID is an operator decision tied to the public Keycloak host and
WebAuthn origin. Never deploy `localhost` as a production value. Decide the RP ID
before enrollment; changing it later can make existing credentials unusable.
Use an environment-specific realm, exact HTTPS origins, a pilot rollout and a
documented recovery procedure. See [Studio authentication](../../docs/runbooks/studio-authentication.md).
