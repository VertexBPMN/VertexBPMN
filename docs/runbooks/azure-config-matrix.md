# Azure-Konfigurationsmatrix (generisch)

> **Open-Source-Hinweis:** Diese Datei ist **generisch** und enthält **keine**
> realen Ressourcen-/Tenant-Kennungen. Sie beschreibt, *welche* Konfiguration pro
> Umgebung existiert und woher die Werte kommen. Konkrete Namen/Secrets liegen in
> der privaten Deployment-Schicht (z. B. `infra/params/<env>.local.bicepparam`,
> git-ignored) und im Key Vault — niemals hier.

Bezug: Umsetzungsplan P6 (Produktionskonfiguration, Identity, sichere Exposition).
Die Werte werden als Environment-Variablen der Container Apps gesetzt und per
`secretref:<name>`-Auflösung (App-seitiger `KeyVaultSecretReference`-Provider) aus
dem Key Vault aufgelöst.

## Umgebungen

| Umgebung | `OperationalMode` | Einstieg | Anmerkung |
|---|---|---|---|
| `dev` | `Development` | lokal | keine Azure-Zwangskonfiguration |
| `stage` | `Stage` | Studio extern, API intern | Validierungsumgebung |
| `prod` | `Production` | Studio + API öffentlich (Entscheidung P6.6 offen) | harte Zwangskonfiguration |

## Pro Anwendung

### API (`<prefix>-api`, ingress: internal/VNet-only in Stage)

| Env-Variable | Quelle | Stage | Prod |
|---|---|---|---|
| `ASPNETCORE_HTTP_PORTS` | Bicep (containerPort) | `8080` | `8080` |
| `Database__ApplyMigrationsOnStartup` | Bicep | `false` | `false` (erzwungen) |
| `Modules__Plugins` | Bicep | `false` | `false` |
| `OperationalMode` | Bicep (aus `environment`) | `Stage` | `Production` |
| `KeyVault__Uri` | Bicep | vault-URI | vault-URI |
| `AZURE_CLIENT_ID` | Bicep (UAMI clientId) | MI | MI |
| `DataProtection__Provider` | Bicep | `AzureBlobKeyVault` | `AzureBlobKeyVault` |
| `DataProtection__BlobUri` | Bicep | blob-URI | blob-URI |
| `DataProtection__KeyVaultKeyIdentifier` | Bicep | key-ID | key-ID |
| `Jwt__SecretKey` | KV `secretref:jwt-secret-key` | KV | KV |
| `Jwt__Audience` | Bicep | `vertexbpmn-api` | `vertexbpmn-api` |
| `ConnectionStrings__<ctx>` (6×) | KV `secretref:pg-*` | KV | KV |
| `Runtime__Outbox__Enabled` / `Provider` / `...` | Bicep | ASB | ASB |
| `Runtime__Outbox__ManagedIdentityClientId` | Bicep | MI clientId | MI clientId |
| `Runtime__Inbox__Enabled` / `Subscription` / `...` | Bicep | ASB | ASB |
| `Runtime__Inbox__ManagedIdentityClientId` | Bicep | MI clientId | MI clientId |

### Studio (`<prefix>-studio`, ingress: external)

Wie API; zusätzlich:

| Env-Variable | Quelle | Anmerkung |
|---|---|---|
| `ApiBaseUrl` | Bicep | Basis-URL der API (intern) |
| `StudioAuthentication:Authority` / `ClientId` / `ClientSecret` | KV/priv. Schicht | OIDC (P6.3); `secretref:` |
| `StudioAuthentication:ApiScope` / `ClaimsScope` | priv. Schicht | Scopes |
| `OidcSessionStore:Provider` / `ConnectionString` | KV | geteilter Session-Store (P4.5) |
| `ReverseProxy:Enabled` / `KnownProxies` | priv. Schicht | ACA-Proxy-Strategie (P6.5) |

### Agent Worker (`<prefix>-agent-worker`, ingress: internal/ohne)

Outbox/Inbox-Konfiguration analog API; keine OIDC-/Studio-Anteile. Migration-Nur.

### Migration Job (`<prefix>-migrate`, Container Apps Job)

| Env-Variable | Quelle | Anmerkung |
|---|---|---|
| `OperationalMode` | Bicep | `Stage` | `Production` |
| `KeyVault__Uri` / `AZURE_CLIENT_ID` | Bicep | KV + MI |
| Connection-Strings | KV `secretref:pg-*` | alle 6 DBs |
| `Jwt__SecretKey` | KV | falls Validierung startet |
| `Runtime__Outbox__...` / `Inbox__...` | Bicep | ASB, MI clientId |

Lauf: `dotnet <api-dll> --migrate-only` (schlägt nach DI-Aufbau fehl, ohne
Hosted-Services auszuführen); Serienkonfiguration: exakt eine Instanz.

## Key-Vault-Referenzen (Namen; Werte nur im Vault)

- `pg-bpmn`, `pg-tenants`, `pg-simulation`, `pg-processminingevents`,
  `pg-decision`, `pg-registry`
- `oidc-session-store-connectionstring`
- `jwt-secret-key` (≥ 32 Byte), `jwt-audience`
- `pg-admin-password` (Server-Admin; nur Deployment-Zeit)

## Erzwungene Produktionshärtung (P6.2)

Siehe `infra/modules/containerapp.bicep` (Env-Assembly) und `main.bicep`
(`applyMigrationsOnStartup: false` je App; `environment`-Param-Werte
whitelisted). `OperationalMode`, `ApplyMigrationsOnStartup=false`,
`Modules__Plugins=false` werden aus dem `environment`-Param abgeleitet/erzwungen,
nicht aus einer locker committeten Config.

## Offene Entscheidungen (P6.3 / P6.6 / P6.7)

- Finaler OIDC-Provider + öffentliche Studio-Domain + Redirect-/Logout-URIs.
- Front Door/WAF ja/nein als öffentlicher Einstieg.
- Keycloak-Selbstbetrieb in Azure (eigener Prod-Betrieb).
