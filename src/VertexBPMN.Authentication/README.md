# Shared server-side OIDC sessions

`VertexBPMN.Authentication` extracts Studio's session/token persistence and cookie-refresh
services without extracting its UI, circuits, account endpoints or protocol configuration.
Existing `VertexBPMN.Studio.Services` type namespaces and migration identifiers are deliberately
retained for source compatibility. There is no project dependency on Studio or ServiceDefaults.
The existing claim-normalization helper is private to this assembly.

```csharp
using VertexBPMN.Authentication;
using VertexBPMN.Studio.Services; // compatibility namespace

// Development only:
services.AddVertexBpmnOidcSessions();

// Relational replicas (SQLite single-host tests; PostgreSQL for multi-host operation):
services.AddVertexBpmnPersistentOidcSessions(options =>
    OidcSessionStoreProvider.Configure(options, "npgsql", connectionString));
```

Hosts own ASP.NET Core `AddCookie` / `AddOpenIdConnect`, Authorization Code + PKCE,
client configuration, cookie names, antiforgery, login/logout routes and Data Protection.
Configure cookie `EventsType = typeof(OidcCookieRefreshEvents)` and call the token store's
`Register` only from the validated OIDC ticket-received event, with a new random session ID.
Set **different** Data Protection application names/key locations and session databases for
Studio and Portal. Replicas of the **same** host share its keys and relational session store.
Never register this server assembly in the WASM client or return its tokens to the browser.
The existing migrations move with the DbContext; run schema migration explicitly via the
host's operational migration path, not inside these registrations.

Refresh reads shared state before using cached tokens, snapshots the original revision before
network I/O and commits with EF database concurrency fencing. Logout clears the encrypted
payload and increments revision, leaving an unreadable tombstone. A stale refresh therefore
cannot reinsert a deleted row. Logout retries a concurrent rotation until revocation commits.
Expired-session cleanup also leaves tombstones. Tombstones intentionally remain: safe bounded
retention needs a separately designed maximum-cookie/session-age policy before physical deletion.

## Local evidence

`tests/VertexBPMN.Authentication.Tests` uses xUnit v3 and executes real file-backed SQLite
with two independent token/session-store instances and two Data Protection providers over the
same filesystem key ring. OIDC refresh responses use signed JWTs validated by the original
ASP.NET/IdentityModel token handler; the deterministic test backchannel is not a real IdP.

Behavioral RED/GREEN logs are in `/home/azureuser/.hermes/cache/scratch/lc06-auth-logs/`:
`red-logout`, `red-stale`, `red-rejected`, `red-fence`, `red-memory`, `red-hydration`,
`red-logout-race` (each `.log`), followed by corresponding green logs or final green.
Final run: **21 total, 0 errors, 0 failed, 0 skipped**; analyzer build: 0 warnings/errors.
Includes 11 unchanged Studio regressions source-linked into this isolated project.

Open acceptance: coordinated Studio/main-suite build, real Keycloak/browser flows, real
PostgreSQL replicas, Azure DP keys and target-environment deployment. SQLite evidence is
single-host relational/crypto concurrency evidence, not multi-host production acceptance.
NuGetAudit was disabled only on restore/build invocations; no dependency vulnerabilities
were fixed by this extraction.
