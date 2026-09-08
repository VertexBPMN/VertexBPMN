# Produktionsprofil – Arbeitsstand

Stand: 2026-09-08. Phase 0 begonnen (Fortsetzung auf Basis `master` @ `296366e`), noch keine Produktionsfreigabe.

## Technisch belegte Grundlage

- .NET 10; `global.json`: SDK 10.0.302 mit latestPatch. Letzter verifizierter Gesamtlauf: SDK 10.0.303, PostgreSQL 17.11 (`postgres:17-alpine`), RabbitMQ 4.3.5.
- Lokale Abnahme auf Windows mit WSLC: PostgreSQL 17 auf 127.0.0.1:55432 und RabbitMQ 4 auf 127.0.0.1:55672; WSLC- und Existing-Infrastruktur über `scripts/test-studio-e2e.ps1/.sh` (`-Infrastructure Wslc|Existing`).
- GUI-Abnahme bleibt lokal; keine Erweiterung des schnellen GitHub-Workflows.
- AppHost-Launchprofile sind Development-Profile, keine Produktionskonfiguration.
- `docs/runbooks/production-deployment.md` beschreibt Kubernetes (getrennter Migrations-Job), externe Datenbanken/Broker und einen gemeinsamen Data-Protection-Keyring. Kubernetes ist eine vorhandene Option, keine vorausgesetzte Nutzerentscheidung.
- Fünf Engine-Stores + Dependency-Registry müssen gemeinsam im Recovery-Konzept berücksichtigt werden. Die im Runbook beschriebene SQLite-Registry ist für konkurrierende Schreibzugriffe gesondert zu qualifizieren.


## Vor einer zielgebundenen Abnahme festzulegen

| Entscheidung | Aktueller Stand | Konsequenz |
|---|---|---|
| Produktionshosting, Betriebssystem und Ressourcen | offen, Nutzerentscheidung ausstehend (Kubernetes laut Runbook eine Option) | Keine Zielumgebungsfreigabe |
| PostgreSQL/RabbitMQ als Produktionsprofil | Zielprofil: PostgreSQL + RabbitMQ (Phase 1 qualifiziert); alternative Provider offen | Andere Produktionsprovider benötigen eigene Abnahme |
| API-/Worker-Replikazahl | **2 API-Replikas + 1 Worker** (2026-09-08) | HA und konkurrierende Registry-Schreibzugriffe zugesagt, müssen in Phase 4 geprüft werden |
| Gleichzeitige Benutzer, Prozessstarts/s, wartende Instanzen, Datenwachstum | **mittel: 1–10 Starts/s, 50–500 parallele Benutzer** (2026-09-08); wartende Instanzen/Historienwachstum noch zu quantifizieren | Kapazitätsprofil (Phase 6) darauf ausrichten |
| Verfügbarkeit, API-p95/p99, Timer-Verzögerung | **Verfügbarkeit 99,5 %; API-p95 < 1 s; p99 < 3 s** (2026-09-08); **maximale Timer-Verzögerung: noch offen** (Vorschlag p95 ≤ 30 s) | Performance-Grenzwerte für diese Werte abnahmefähig; Timer-Wert noch vereinbaren |
| RTO und RPO | **RPO ≤ 15 min; RTO ≤ 4 h** (2026-09-08) | Restore kann gegen Betriebsziel freigegeben werden (Phase 5 misst) |
| Identity Provider, Secret Store, Keyring, TLS-Terminierung | offen, Nutzerentscheidung ausstehend | Produktionsauthentifizierung separat abzunehmen (Phase 3) |
| Backupintervall, Aufbewahrung, Verantwortliche und Alarmempfänger | offen | Pilotfreigabe bleibt offen (Phase 8) |


## Erste Nachweismatrix

| Bereich | Vorhandener Beleg | Nächste Prüfung |
|---|---|---|
| OAuth2-Service | `tests/VertexBPMN.Tests/Unit/Infrastructure/OAuth2CredentialFlowServiceTests.cs` verwendet InMemory | Bestehende Logiktests ausführen; relationale Migration separat prüfen |
| OAuth2-API | `tests/VertexBPMN.Tests/Integration/Api/OAuth2FlowApiTests.cs` | Zielgerichtet ausführen |
| PostgreSQL-Upgrade | neue `OAuth2PostgresMigrationTests` | Typfehler reproduzieren, Folgemigration, UTC-Erhalt und Cleanup |
| Externe Betriebsverträge | `ExternalBrokerPhase3AcceptanceTests.cs` enthält feste Fact-Skips | Phase 2 muss ausführbaren Opt-in und Pflichtfallzählung herstellen; bisherige grüne Doku reicht nicht |
| UI | Phase-7-Abnahmebericht und Getränke-Roundtriptest | Finalen Release-Kandidaten mit realer Persistenz erneut lokal abnehmen |

## Nachweis-Inventur (Phase 0, Punkt 4)

Inventur-Stand: Master @ `296366e`. „Letzter geprüfter Commit“ = Stand des verifizierten gebündelten Laufs (`0dc01e3d…`, Basis `e4b3949` + uncommittete Korrekturen, 2026-09-08: 1005 bestanden, 0 Fehler, 0 Skips). Der aktuelle `master` (`296366e`, inkl. Studio-Redesign) ist noch **nicht** in einem solchen gebündelten Lauf nachqualifiziert → Eingang der verbleibenden Phase-2-Punkte. Kein Häkchen ohne versionsgebundenen, frischen Nachweis.

| Bereich | Öffentlicher Einstieg | Persistenz | Stellvertretende Tests | Letzter geprüfter Commit | Offene Grenze |
|---|---|---|---|---|---|
| Engine-Laufzeit (BPMN, Ereignisse, Timer, Tasks, Gateways, Multi-Instance, Boundary) | `RuntimeController`, `PersistentProcessExecutionRuntime` | 5 Stores + Outbox/Broker | `PersistentRuntimePhase2AcceptanceTests`, `LocalStudioInfrastructureTests.{BoundaryEvents,Events,Tasks,Gateways,MultiInstance,SubProcesses,DataArtifacts}`, `ProcessEngineTests` | `e4b3949`+Fixes | Finale Abnahme ab sauberem Checkout; Transaktions-/Ausfallfälle (Phase 4) |
| REST-API | 59 Controller unter `src/VertexBPMN.Api/Controllers` | jeweils zugeordnete Stores | `Integration/Api/*` (Process, Task, Timer, Migration, Message, Signal, Credential, Decision, Health, …) | `e4b3949`+Fixes | Endpunkt-Inventur vs. Rollen-/Tenant-Matrix (Phase 3) |
| gRPC | `vertexbpmn.proto` (+ MCP) | Engine-Stores | `GrpcContractTests` | `e4b3949`+Fixes | Auth-Z/Abdeckung (Phase 3) |
| SDK (.NET) | `VertexBPMN.Sdk` | via API | `VertexBpmnClientTests`, `docs/reference/sdk-dotnet.md` | `e4b3949`+Fixes | Release-Paket-Test aus frischer Umgebung (Phase 8) |
| CLI (`dashboard`/Workflow) | `VertexBPMN.Cli` | via API | `CliApplicationTests` | `e4b3949`+Fixes | Paket-/Installtest (Phase 8) |
| Studio (Blazor + bpmn-io) | `VertexBPMN.Studio` | via API, Formulare/Patterns | `StudioUiContractTests`, `StudioUiAcceptanceTests`, `StudioVisualBaselineTests`, `BpmnEditorInsertionTests`, `LocalStudioInfrastructureTests.Editor*` | `e4b3949`+Fixes (Studio-Redesign danach auf `master`, nicht nachqualifiziert) | Finale UI-Gesamtabnahme des neuen Designs; Editor-Accessibility |
| DMN (FEEL/SRD) | `DecisionController`, `VertexDecision*` | Decision-Store | `DmnDecisionTableTests.*`, `DmnFeelDrdFullSupportAcceptanceTests`, `DmnEngineTests`, `DmnTckRunner` | `e4b3949`+Fixes | DMN-TCK-Vollabnahme versionsgebunden (Phase 7) |
| CMMN | `CaseDefinitionController`, `GrpcCaseManagementService` | Bpmn/Case-Stores | `CmmnParserTests`, `CmmnHistoryPersistenceTests` | `e4b3949`+Fixes | Live-Akzeptanz (Phase 7) |
| Connectors/Handler | `ConnectorController`, Service-Task-Handler | Credential-Store | `ConnectorRuntimeTests`, `PersistentConnectorServiceTests`, `AiServiceTaskHandler*`, `SendGridServiceTaskHandlerTests` | `e4b3949`+Fixes | Externe Ziele/Targets, Redirects, Limits (Phase 3) |
| MCP | `vertexbpmn_mcp.proto`, McpAdapter | via API | `Mcp*Tests` (Server, Client, Agent, Handler) | `e4b3949`+Fixes | Endgeräte/Plugins (Phase 3/7) |
| OAuth2 & Migration | `OAuth2Controller`, `MigrationController`, EF-Migrationen | Bpmn-Store | `OAuth2PostgresMigrationTests` (13/13 grün), `OAuth2FlowApiTests`, `OAuth2CredentialFlowServiceTests`, `LiveProcessMigrationAcceptanceTests`, `SchemaVerificationTests` | neu verifiziert (Migration 20260908090000) | Echter IdP (Phase 3); weitere zugesagte Provider |
| Sicherheit/Rollen/Tenant | `IdentityController`, `VertexAuthorizationController`, JWT | Tenant-Store | `EnhancedSecurityTests`, `TaskAndAnalyticsSecurityTests`, `OperationalReadinessPhase3AcceptanceTests` | `e4b3949`+Fixes | Vollständige Rollen-/Tenant-Matrix, IdP, Secrets (Phase 3) |
| Betrieb/Health/Metriken | `/api/health/live`, `/api/ready`, `/api/metrics/prometheus` | Infrastruktur | `HealthEndpointTests`, `ServiceDependenciesHealthCheckTests`, `ConfigurationSecretProviderTests` | `e4b3949`+Fixes | Betriebszielwerte (Phase 0), Alarme (Phase 8) |
| Konformität | SDK/CLI, Parsing/Serialisierung | – | `DistributedConformanceTests`, MIWG-Bericht, `Roundtrip/StrictPhase*`, `Parsing/Validation/*` | `e4b3949`+Fixes | DMN-TCK-Volllauf versionsgebunden (Phase 7) |
| Last/Dauerbetrieb | API/Runtime | alle Stores | `PerformanceRunner/Smoke`, `Phase12EcosystemTests` (Ansätze) | kein belastbarer Nachweis | Gesamtes Phase-6-Paket (Szenarien, p95/p99, 24–72 h) |

Bewusste bekannte Grenzen (kein vollständiger Nachweis): Scripting – JavaScript/Jint ist ressourcenbegrenzt (2s/8 MB), C#/Roslyn ist gesandbox-schwach und nur explizit (`scriptFormat=C#`) aktiv; verwalte `Runtime:Scripts:Enabled` für untrusted Tenants. Dependency-Registry (SQLite) unter Konkurrenz (Phase 4). Reale GPU/LLM-Anbindungen und echte IdP-/Secret-Provider gehören zu Phase 3.

Keine Secrets oder produktiven Verbindungsdaten in dieses Dokument aufnehmen.

## Verifikation des ersten Umsetzungsschritts

- Folgemigration `20260908090000_NormalizePostgresOAuth2FlowStateTimes` konvertiert CreatedAt/ExpiresAt ausschließlich für PostgreSQL. Offsetfreie Altwerte werden ausdrücklich als UTC interpretiert; explizite Offsets bleiben erhalten. Ungültige Zeitwerte führen zum Transaktionsabbruch statt zu stiller Löschung; ein separater Fehlerdaten-Test steht noch aus.
- `OAuth2PostgresMigrationTests` prüft echte Neuinstallation und Upgrade aus der ursprünglichen OAuth2-Migration. Vor dem Upgrade wird der ursprüngliche PostgreSQL-Typfehler erwartet, danach ein bestehender State mit UTC-/Offsetwerten gelesen sowie aktiver und abgelaufener State geschrieben. Cleanup löscht ausschließlich den abgelaufenen State.
- Der SQLite-Upgradefall prüft den unveränderten alternativen Providerpfad.
- Release-Build von `tests/VertexBPMN.Tests` erfolgreich: 0 Fehler, 15 vorhandene Warnungen außerhalb der neuen Dateien.
- `dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~OAuth2"` mit lokal gesetztem `VERTEXBPMN_TEST_POSTGRES_ADMIN`: **12 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Davon drei neue Migrationsfälle; vorhandene Service-/API-Tests sind ergänzende, nicht pauschal PostgreSQL-basierte Nachweise.
- Testdatenbanken besitzen zufällige Namen und werden im finally-Block gelöscht. Keine bestehenden Anwendungsdatenbanken migriert.
- Ergänzende Abnahme: **13 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Die drei PostgreSQL-Szenarien prüfen jetzt zusätzlich Start, Callback, verschlüsselte Token-Persistenz, sequenzielles Replay, unbekannten/abgelaufenen State und fremden Tenant. Nur der externe Token-Endpunkt wird durch einen kontrollierten HTTP-Handler ersetzt; der produktive Service und Credential-Store verwenden echtes PostgreSQL. Kein Nachweis konkurrierender Callbacks oder eines echten Identity Providers.
- Ungültiger ExpiresAt-Altwert: PostgreSQL lehnt die Migration mit InvalidDatetimeFormat ab; Migrationshistorie, Datensatz und CreatedAt-Typ bleiben unverändert. Nach expliziter Reparatur des Testwerts gelingt die Migration. Es erfolgt keine automatische Löschung ungültiger Daten.
- Der bestehende lokale Port 55432 zeigte vor den Assertions Verbindungs-/Authentifizierungstimeouts trotz erfolgreichem pg_isready im Container. Die unveränderten Tests bestanden gegen einen frischen isolierten WSLC-Container desselben PostgreSQL-17-Images auf Port 55439. Der temporäre Container wurde anschließend entfernt. Die Ursache des bestehenden Portproblems ist nicht abschließend diagnostiziert und bleibt ein lokaler Infrastruktur-Befund.
- Noch offen: weitere Produktionsprovider nach Profilentscheidung, echte Identity-Provider-Anbindung, konkurrierende Callbacks (Sicherheits-/Konkurrenzabnahme), Betriebszielwerte und endgültige Zielumgebungsfreigabe. Der PostgreSQL-Migrationsfehler und die vorgesehenen sequenziellen Servicefälle sind qualifiziert; Phase 0 bleibt offen.
