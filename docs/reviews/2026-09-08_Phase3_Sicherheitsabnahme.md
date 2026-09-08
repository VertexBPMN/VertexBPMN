# Phase 3 – Sicherheitsabnahme (Abnahmebericht)

Datum: 2026-09-08 · Kandidat: `master` (Arbeitsstand nach Phase-3-Remediation) · Zielprofil: siehe `2026-09-08_Produktionsprofil.md`

## 1. Ergebnis

Sicherheitsabnahme **durchgeführt**. Die zwei offenen **Hoch-Befunde** (Roslyn-C#-RCE und `Runtime:Scripts:Enabled`-Default) wurden beseitigt, die höchsten Cross-Tenant-/ID-Zugriffslücken (T1–T4) geschlossen und mit Negativtests belegt. Dependency-Audit ist gegen erreichbare Quellen grün (0 Befunde). **Ausstehend** (externe Ressourcen, nicht autonom lösbar): Req 2 (Prüfung gegen echten Identity Provider) und Req 6 (unabhängiges Sicherheitsreview).

Abnahmekriterium *„keine offenen ausnutzbaren kritischen/hohen Befunde; wirksame Tenant-/Rollengrenzen und Secret-Behandlung durch positive und negative Fälle belegt“* ist für die **behebbaren** Bezugspunkte erfüllt. Ein vollständiger Abschluss der Phase hängt an Req 2/Req 6.

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
- **M1 – Keine Redirect-Policy im Connector-Client** → SSRF-Bypass über 30x-Umleitungen möglich. Bewertung: Kern-SSRF-Guard (Private-IP-Block + Host-Allowlist) ist vorhanden; Umleitungsverfolgung umgeht diesen teilweise. **Empfehlung:** Redirect-Handler ergänzen, vor Freigabe.
- **M2 – OAuth2 Authorization-/Token-URL unterliegt nicht dem SSRF-Guard.** Bewertung: ausgenutzer Zielserver/Operator; vor Freigabe zu behandeln.
- **M3 – BPMN-Redaction default aus, nicht auf Export-/Roh-XML-Pfad.** Bewertung: Redaktions-Policies sind als Parse-Option vorhanden und getestet; für Export-Pfad nachrüsten.
- **M4 – Webhook kein expliziter Replay-Schutz.** Bewertung: HMAC + Einmal-Secret vorhanden; Replay-Fenster optional.
- **M5 – DNS-Rebinding/TOCTOU nicht in Redirect-/Hostprüfung mitigiert.**
- **M6 – OAuth2-State nicht an den Benutzer gebunden.**

### Cross-Tenant-/ID-Lücken (behoben, T1–T4)
- **T1 `VertexJobController`** — war ohne Rollen-Policy und ohne Tenant-Einschränkung (jeder Authentifizierte listete alle Jobs). **Fix:** `[Authorize(Policy="ReadOnly")]` + Tenant-Filter (Admin sieht alle, Non-Admin nur eigene). Negativtests: 3.
- **T2 `VertexVariableController`** — Variablen per `processInstanceId` ohne Tenant-Prüfung. **Fix:** Instance-Tenant-Check vor dem Auslesen, `NotFound` bei Fremd-Tenant (kein Existence-Oracle), kein `GetVariablesAsync`-Aufruf. Negativtests: 2.
- **T3 `SimulationScenario`/`Simulation`** — Szenarien ohne Tenant-Schutz inkl. Fremd-Tenant-Create. **Fix:** `[Authorize]` + Tenant-Pinning (Non-Admin auf Claim-Tenant fixiert, Admin darf Query-/Body-Tenant), Access-Checks mit `NotFound`. Negativtests: 3.
- **T4 `TaskIoSnapshot`** — akzeptierte beliebige Query-`tenantId`. **Fix:** Non-Admin auf Claim-Tenant fixiert, Admin darf Query-Tenant. Negativtests: 2 (inkl. In-Memory-EF).

### Rollen-Lücken S1–S6 (zurückgestellt, bewertet)
Management suspend/delete, Health GC-/RateLimit-Reset, LoadBalancer, Metrics/Performance, Identity list-tenants global: **Mittel**, Rollen-Härtung z. B. auf `AdminOnly` empfehlenswert vor Produktivfreigabe; ohne unbefugte Datenausleitung (kein Cross-Tenant) bewertet.

### Bereits verifiziert vorhanden / unverändert
Jint-Sandbox 2 s/8 MB · Connector-SSRF (Private-IP-Block 10/8, 172.16/12, 192.168/16, Loopback, Link-Local + Host-Allowlist) · constant-time HMAC/Vergleich · fehlendes Secret abgelehnt · OAuth2-State (32-Byte-Nonce, TTL-Cleanup, Rotation) · BpmnRedaction · XXE-Block (secure XML).

## 4. Offene Punkte (Blockierung des Phasenabschlusses, extern)

- **O1 / Req 2 – echter Identity Provider:** Login, Logout, abgelaufene Tokens, Rollenentzug, verweigerte Zugriffe gegen einen echten IdP. Hängt an der Phase-0-IDP-Entscheidung (aktuell offen). Externer Token-Endpunkt ist simuliert getestet.
- **O2 / Req 6 – unabhängiges Sicherheitsreview** des finalen Kandidaten für geschäftskritischen Einsatz. Organisatorisch; externer Review-Partner nötig.

## 5. Dateien/Änderungen dieser Phase
- `src/VertexBPMN.Application/RepositoryService.cs` — `Runtime:Scripts:AllowCSharp`-Gate (H1/H2)
- `src/VertexBPMN.Api/Controllers/VertexJobController.cs`, `VertexVariableController.cs`, `SimulationScenarioController.cs`, `SimulationController.cs`, `TaskIoSnapshotController.cs` — Tenant-/Rollen-Fixes (T1–T4)
- `tests/VertexBPMN.Studio.UiTests/LocalStudioE2ETestHost.cs` — C#-Flag in vertrauenswürdiger Testumgebung
- `tests/VertexBPMN.Tests/Unit/Application/RepositoryServiceScriptGateTests.cs` — Gate-Tests (neu)
- `tests/VertexBPMN.Tests/Unit/Api/TenantIsolationPhase3SecurityTests.cs` — Negativtests T1–T4 (neu)
- Audit-Drafts: `2026-09-08_Phase3_TenantRollenMatrix_draft.md`, `2026-09-08_Phase3_ScriptConnectorBoundaries_draft.md`

## 6. Empfohlene nächste Schritte
1. M1 (Connector-Redirect-Policy) als nächstes beheben — schließt den wirksamsten SSRF-Bypass.
2. Rollen-Härtung S1–S6 (AdminOnly) angehen.
3. Echten IdP für Req 2 bereitstellen (Phase-0-Entscheidung).
4. Externes Sicherheitsreview (Req 6) organisieren.
