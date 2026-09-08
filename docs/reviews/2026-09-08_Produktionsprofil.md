# Produktionsprofil – Arbeitsstand

Stand: 2026-09-08. Phase 0 begonnen, noch keine Produktionsfreigabe.

## Technisch belegte Grundlage

- .NET 10; `global.json`: SDK 10.0.302 mit latestPatch. Lokaler Build verwendet 10.0.303.
- Lokale Abnahme auf Windows mit WSLC: PostgreSQL 17 (`postgres:17-alpine`) auf 127.0.0.1:55432 und RabbitMQ 4 auf 127.0.0.1:55672.
- Bestehende lokale Dienste können über `scripts/test-studio-e2e.ps1 -Infrastructure Existing` eingebunden werden; WSLC wird über `-Infrastructure Wslc` unterstützt.
- GUI-Abnahme bleibt lokal; keine Erweiterung des schnellen GitHub-Workflows.
- AppHost-Launchprofile sind Development-Profile, keine Produktionskonfiguration.
- `docs/runbooks/production-deployment.md` beschreibt Kubernetes, externe Datenbanken/Broker und einen gemeinsamen Data-Protection-Keyring. Das ist eine vorhandene Deploymentoption; die tatsächliche Hostingentscheidung ist offen.
- Fünf Engine-Stores und Dependency-Registry müssen gemeinsam im Recovery-Konzept berücksichtigt werden. Die im Runbook beschriebene SQLite-Registry ist für konkurrierende Schreibzugriffe gesondert zu qualifizieren.

## Vor einer zielgebundenen Abnahme festzulegen

| Entscheidung | Aktueller Stand | Konsequenz |
|---|---|---|
| Produktionshosting, Betriebssystem und Ressourcen | offen, Nutzerentscheidung angefragt | Keine Zielumgebungsfreigabe |
| PostgreSQL/RabbitMQ als Produktionsprofil oder alternative Provider | lokal bestätigt, Produktion offen | Aktuelle Korrektur qualifiziert PostgreSQL; andere Produktionsprovider benötigen eigene Abnahme |
| API-/Worker-Replikazahl | offen | HA und konkurrierende Registry-Schreibzugriffe noch nicht zugesagt |
| Gleichzeitige Benutzer, Prozessstarts/s, wartende Instanzen, Datenwachstum | offen | Kein belastbares Kapazitätsversprechen |
| Verfügbarkeit, API-p95/p99, Timer-Verzögerung | offen | Performance-Grenzwerte noch nicht abnahmefähig |
| RTO und RPO | offen | Restore kann technisch getestet, aber noch nicht gegen Betriebsziel freigegeben werden |
| Identity Provider, Secret Store, Keyring, TLS-Terminierung | offen | Produktionsauthentifizierung separat abzunehmen |
| Backupintervall, Aufbewahrung, Verantwortliche und Alarmempfänger | offen | Pilotfreigabe bleibt offen |

## Erste Nachweismatrix

| Bereich | Vorhandener Beleg | Nächste Prüfung |
|---|---|---|
| OAuth2-Service | `tests/VertexBPMN.Tests/Unit/Infrastructure/OAuth2CredentialFlowServiceTests.cs` verwendet InMemory | Bestehende Logiktests ausführen; relationale Migration separat prüfen |
| OAuth2-API | `tests/VertexBPMN.Tests/Integration/Api/OAuth2FlowApiTests.cs` | Zielgerichtet ausführen |
| PostgreSQL-Upgrade | neue `OAuth2PostgresMigrationTests` | Typfehler reproduzieren, Folgemigration, UTC-Erhalt und Cleanup |
| Externe Betriebsverträge | `ExternalBrokerPhase3AcceptanceTests.cs` enthält feste Fact-Skips | Phase 2 muss ausführbaren Opt-in und Pflichtfallzählung herstellen; bisherige grüne Doku reicht nicht |
| UI | Phase-7-Abnahmebericht und Getränke-Roundtriptest | Finalen Release-Kandidaten mit realer Persistenz erneut lokal abnehmen |

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
