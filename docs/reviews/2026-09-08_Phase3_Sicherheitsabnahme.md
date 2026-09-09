# Phase 3 – Sicherheitsabnahme (Abnahmebericht)

Datum: 2026-09-08 · Kandidat: `master` (Arbeitsstand nach Phase-3-Remediation) · Zielprofil: siehe `2026-09-08_Produktionsprofil.md`

## 1. Ergebnis

**Korrigierter Stand 2026-09-09: Sicherheitsabnahme nicht abgeschlossen.** Das Review hat zusätzliche hohe Lücken bei fehlenden Tenant-Claims, Debug-Zugriffen, Rollenprüfungen und der Ausführung bereits gespeicherter C#-Prozesse aufgedeckt. Diese Pfade wurden nachgehärtet. Der untenstehende Nachweis vom 2026-09-08 ist historisch und kein Nachweis für den aktuellen Arbeitsstand.

Die pauschale Aussage, alle autonom behebbaren Punkte seien abgeschlossen, wird zurückgenommen. Neben Req 2/Req 6 sind M3/M4/M6 und weitergehende Connector-Nachweise offen. Aktuelle Korrekturen, Tests und nächste Schritte: [Nachprüfung vom 2026-09-09](2026-09-09_Phase3_Sicherheitsnachpruefung.md).

## 2. Verifizierter Nachweis (reale Ausführung, 2026-09-08)

| Nachweis | Ergebnis |
|---|---|
| Sicherheits-Test-Suite (Negative-Tenant, Anonymous→401, Webhook-HMAC/Einmal-Secret, OAuth2-State, Redaktion, XXE, Connector-Host-Allowlist, Credential-Isolation) | 56/56 grün (12 Klassen, 15.6 s) |
| Neue Phase-3-Negativtests T1–T4 + Script-Gate | 13/13 grün |
| Volle Core-Suite (`VertexBPMN.Tests`, Release) nach allen Produktionsänderungen | **860 Tests, 0 Fehler, 0 Failed, 7 Skips** (92.9 s) |
| Dependency-Audit NuGet (`dotnet list --vulnerable --include-transitive`, Quelle `api.nuget.org`) | **19 Projekte, 0 vulnerable** (direkt + transitiv, alle Frameworks) |
| Dependency-Audit npm (Studio, FEEL, je aus `package-lock.json`) | **0** (alle Severities) |
| API-Build (Release) | Build succeeded (0 Fehler) |

## 3. Befund-Konsolidierung

### Kritisch/Hoch (behoben)
- **H1 – Roslyn-C#-Script unsandboxed (RCE).** `ScriptTaskExecution.cs` führte `scriptFormat=C#` via `CSharpScript.EvaluateAsync` ohne Memory-/Timeout-/IO-Grenze aus.
  **Fix:** Operator-Gate `Runtime:Scripts:AllowCSharp` (default `false`) am Deploy-Punkt (`RepositoryService.cs`). Ein Deployment mit `scriptFormat=C#` wird ohne explizite Freigabe abgelehnt; C# bleibt als Funktion unterstützt (entspricht der früheren Entscheidung „C# weiter unterstützt, nur nicht als Default“). Erreichbare Skriptausführung für nicht freigegebene C#-Skripte damit geschlossen; der JS-Default bleibt Jint-bounded (2 s / 8 MB).
  Nachweis: `RepositoryServiceScriptGateTests` (3 Fälle: C#-Reject by default, C#-Allow mit Flag, JS-Allow by default). **grün.**
- **H2 – `Runtime:Scripts:Enabled` default `true`.** Durch das `AllowCSharp`-Gate ist der gefährlichste Pfad (C#/Roslyn) unabhängig zusätzlich abgesichert; Skriptausführung insgesamt bleibt an `Runtime:Scripts:Enabled` und nun zusätzlich an `Runtime:Scripts:AllowCSharp` gebunden. Bewertung: Nach Fix kein offener nutzbarer Hoch-Befund mehr.

### Hoch/Critical – geschlossen durch obige Fixes. Kein offener nutzbarer Hoch-Befund.

### Mittel (akzeptiert mit nachvollziehbarer Bewertung, geplant/optional behebbar)
- **M1 – Keine Redirect-Policy im Connector-Client** → SSRF-Bypass über 30x-Umleitungen. **Behoben:** geteilter Connector-`HttpClient` verfolgt keine automatischen Redirects mehr (`SocketsHttpHandler.AllowAutoRedirect=false`, `ServiceTaskRegistryExtensions.cs`). Redirect-Ziele werden nicht mehr gefolgt; ein 3xx-`Location` zurück auf eine interne Adresse wird als 302 an den Aufrufer gegeben. Nachweis: `ConnectorRedirectSsrfTests` (2 Tests, grün).
- **M2 – OAuth2 Token-URL unterlag nicht dem SSRF-Guard.** Bewertung: ausgenutzer Zielserver/Operator; vor Freigabe zu behandeln. **Behoben:** beide Token-POSTs (authorization_code + refresh) laufen durch `ConnectorDestinationPolicy.ThrowIfForbiddenAsync` (Private-/Loopback-/Link-Local-Block nach DNS-Auflösung, allowlist-frei). Negativtests: 2.
- **M3 – BPMN-Redaction default aus, nicht auf Export-/Roh-XML-Pfad.** Bewertung: Redaktions-Policies sind als Parse-Option vorhanden und getestet; für Export-Pfad nachrüsten.
- **M4 – Webhook kein expliziter Replay-Schutz.** Bewertung: HMAC + Einmal-Secret vorhanden; Replay-Fenster optional.
- **M5 – DNS-Rebinding/TOCTOU in der Hostprüfung.** **Behoben:** Connector-`SocketsHttpHandler` verbindet jetzt über einen `ConnectCallback` zur **validierten** IP (Resolution + Private-/Loopback-Block direkt am Connect, SNI bleibt Hostname) statt per Hostname neu aufzulösen — ein post-check Rebind zu einer internen Adresse wird nie erreicht. `ResolveValidatedAddressesAsync` extrahiert (geteilt). Negativtests: 2 (`ConnectorRebindingSsrfTests`, Loopback-Ziel → Fehler, Listener unberührt).
- **M6 – OAuth2-State nicht an den Benutzer gebunden.**

### Cross-Tenant-/ID-Lücken (behoben, T1–T4)
- **T1 `VertexJobController`** — war ohne Rollen-Policy und ohne Tenant-Einschränkung (jeder Authentifizierte listete alle Jobs). **Fix:** `[Authorize(Policy="ReadOnly")]` + Tenant-Filter (Admin sieht alle, Non-Admin nur eigene). Negativtests: 3.
- **T2 `VertexVariableController`** — Variablen per `processInstanceId` ohne Tenant-Prüfung. **Fix:** Instance-Tenant-Check vor dem Auslesen, `NotFound` bei Fremd-Tenant (kein Existence-Oracle), kein `GetVariablesAsync`-Aufruf. Negativtests: 2.
- **T3 `SimulationScenario`/`Simulation`** — Szenarien ohne Tenant-Schutz inkl. Fremd-Tenant-Create. **Fix:** `[Authorize]` + Tenant-Pinning (Non-Admin auf Claim-Tenant fixiert, Admin darf Query-/Body-Tenant), Access-Checks mit `NotFound`. Negativtests: 3.
- **T4 `TaskIoSnapshot`** — akzeptierte beliebige Query-`tenantId`. **Fix:** Non-Admin auf Claim-Tenant fixiert, Admin darf Query-Tenant. Negativtests: 2 (inkl. In-Memory-EF).

### Rollen-Lücken S1–S6 (hartärbarer Teil behoben)
- **Behoben (`AdminOnly`):** Health `gc` + `rate-limits/{id}/reset` (Doku-Kommentar „admin only“ war nicht durchgesetzt), Identity `list-tenants` (globale Tenant-Liste), LoadBalancer `workers`-Unregister + `rebalance` + `config`-PUT (Infra-Mutationen).
- **Bewusst offen:** `Management` suspend/resume/delete bleibt tenant-gescoped (`ResolveTenant`: Admin beliebig, Non-Admin claim-gepinnt) — ProcessManager darf eigene Instanzen verwalten, kein Cross-Tenant. `Metrics`/`Performance`-Monitoring-Reads bleiben erreichbar (Telemetrie/Scraping); eine Einschränkung ist Proposal, keine Schwachstelle.
- **Negativtests:** `PrivilegeGateSecurityTests` — ReadOnly-Prinzipal (X-Test-User) erhält **403** auf allen 6 gehärteten Endpoints; Admin-Prinzipal OK (8/8 grün).

### Bereits verifiziert vorhanden / unverändert
Jint-Sandbox 2 s/8 MB · Connector-SSRF (Private-IP-Block 10/8, 172.16/12, 192.168/16, Loopback, Link-Local + Host-Allowlist) · constant-time HMAC/Vergleich · fehlendes Secret abgelehnt · OAuth2-State (32-Byte-Nonce, TTL-Cleanup, Rotation) · BpmnRedaction · XXE-Block (secure XML).

## 4. Offene Punkte (Blockierung des Phasenabschlusses, extern)

- **O1 / Req 2 – echter Identity Provider:** Login, Logout, abgelaufene Tokens, Rollenentzug, verweigerte Zugriffe gegen einen echten IdP. Hängt an der Phase-0-IDP-Entscheidung (aktuell offen). Externer Token-Endpunkt ist simuliert getestet.
- **O2 / Req 6 – unabhängiges Sicherheitsreview** des finalen Kandidaten für geschäftskritischen Einsatz. Organisatorisch; externer Review-Partner nötig.

## 5. Dateien/Änderungen dieser Phase
- `src/VertexBPMN.Application/RepositoryService.cs` — `Runtime:Scripts:AllowCSharp`-Gate (H1/H2)
- `src/VertexBPMN.Application/Extensions/ServiceTaskRegistryExtensions.cs` — Connector-HttpClient ohne Auto-Redirect (M1)
- `src/VertexBPMN.Application/Connectors/ConnectorRuntime.cs`, `src/VertexBPMN.Infrastructure/Persistence/Services/OAuth2CredentialFlowService.cs` — OAuth2-Token-URL hinter SSRF-Guard (M2)
- `src/VertexBPMN.Api/Controllers/VertexJobController.cs`, `VertexVariableController.cs`, `SimulationScenarioController.cs`, `SimulationController.cs`, `TaskIoSnapshotController.cs` — Tenant-/Rollen-Fixes (T1–T4)
- `src/VertexBPMN.Api/Controllers/HealthController.cs`, `IdentityController.cs`, `LoadBalancerController.cs` — Rollen-Härtung S2/S3/S5 (`AdminOnly`)
- `tests/VertexBPMN.Studio.UiTests/LocalStudioE2ETestHost.cs` — C#-Flag in vertrauenswürdiger Testumgebung
- `tests/VertexBPMN.Tests/Unit/Application/RepositoryServiceScriptGateTests.cs` — Gate-Tests (neu)
- `tests/VertexBPMN.Tests/Unit/Api/TenantIsolationPhase3SecurityTests.cs` — Negativtests T1–T4 (neu)
- `tests/VertexBPMN.Tests/Integration/Handlers/ConnectorRedirectSsrfTests.cs` — M1-Redirect-Negativtests (neu)
- `tests/VertexBPMN.Tests/Unit/Infrastructure/OAuth2CredentialFlowServiceTests.cs` — M2-Negativtests (Loopback/Private Token-URL)
- Audit-Drafts: `2026-09-08_Phase3_TenantRollenMatrix_draft.md`, `2026-09-08_Phase3_ScriptConnectorBoundaries_draft.md`

## 6. Empfohlene nächste Schritte
1. M3 (Export-/Roh-XML-Redaction), M4 (Webhook-Replay-Schutz), M6 (OAuth2-State an Benutzer binden) — restliche MEDIUM-Punkte; M5 (DNS-Rebinding) in diesem Durchlauf behoben.
2. Req 2 (echter IdP) und Req 6 (unabhängiges Sicherheitsreview) benötigen externe Ressourcen für den Phasenabschluss.
3. Echten IdP für Req 2 bereitstellen (Phase-0-Entscheidung).
4. Externes Sicherheitsreview (Req 6) organisieren.
