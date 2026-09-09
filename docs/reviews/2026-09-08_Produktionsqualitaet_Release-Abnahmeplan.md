# VertexBPMN – Plan für hohe Produktionsqualität

Stand: 2026-09-08. Status: Phase 0/1 in Arbeit; PostgreSQL-OAuth2-Typkorrektur umgesetzt und lokal verifiziert.

Aktueller Nachweis: [Produktionsprofil](2026-09-08_Produktionsprofil.md). Neue Folgemigration `20260908090000_NormalizePostgresOAuth2FlowStateTimes` korrigiert PostgreSQL-Zeittypen ohne Änderung der ursprünglichen Migration. Gezielte OAuth2-Suite: 13 bestanden, 0 fehlgeschlagen, 0 übersprungen. PostgreSQL-Callback, sequenzielles Replay, Tenantgrenzen, Ablauf und ungültige Altdaten sind zusätzlich geprüft. Phase 1 ist für PostgreSQL technisch qualifiziert; weitere Produktionsprovider bleiben profilabhängig offen.

## 1. Ziel und Grenzen

Ein versionierter Release soll geschäftskritische Prozesse in einer definierten Produktionsumgebung zuverlässig, sicher und wiederherstellbar ausführen. Freigabe beruht auf reproduzierbaren Ergebnissen desselben Release-Commits und seiner Artefakte.

Das abgeschlossene Studio-Redesign ist eine Grundlage, keine Gesamtproduktfreigabe. Vorhandene Tests und Implementierungen werden zuerst geprüft und wiederverwendet. Ein fehlender aktueller Nachweis bedeutet nicht automatisch, dass eine Funktion fehlt.

- GUI-Tests bleiben ausschließlich lokal. Der schnelle GitHub-Workflow bleibt schlank.
- Lokale Infrastruktur: WSLC oder vorhandene PostgreSQL-/RabbitMQ-Dienste. Bestehende Docker-/Podman-AppHost-Profile bleiben erhalten.
- Tests verwenden isolierte Datenbanken, Mandanten und Broker-Ressourcen. Ausfalltests laufen niemals gegen produktive oder gemeinsam verwendete Dienste.
- Keine abgeschwächten Assertions, synthetischen Erfolgsergebnisse oder stillschweigend übersprungenen Pflichtprüfungen.
- Ein bestandener lokaler WSLC-Lauf ersetzt keine Abnahme der tatsächlichen Produktionsumgebung.
- Aufwand: S = 1–2, M = 3–5, L = 6–10 Personentage einschließlich Tests und Dokumentation. Schätzungen werden nach Phase 0 aktualisiert; neu entdeckte Fehler sind nicht pauschal eingerechnet.

## 2. Ausgangslage und Belege

| Befund | Beleg | Einordnung |
|---|---|---|
| OAuth2-Ablaufzeit wird als TEXT migriert; PostgreSQL-Zeitvergleich schlug in der lokalen Abnahme fehl. | `src/VertexBPMN.Infrastructure/Persistence/Migrations/Bpmn/20260906175904_AddOAuth2FlowStates.cs`, `docs/reviews/2026-09-08_Studio-UI-Phase-7-Lokale-Gesamtabnahme.md` | Konkreter offener Befund; aktuelle Reproduktion erforderlich. |
| Der Standardworkflow nimmt externe Akzeptanztests aus und führt das separate UI-Projekt nicht aus. | `.github/workflows/ci.yml`, `docs/runbooks/security-and-release-gates.md` | Bewusste CI-Entscheidung; vollständige lokale Release-Abnahme erforderlich. |
| Die Supportmatrix beschreibt externe Tests zugleich als verpflichtendes CI-Gate und als vom Standardworkflow ausgenommen. | `docs/reference/product-support-matrix.md` | Dokumentationswiderspruch korrigieren. |
| Restore-Anleitung und Anforderungen existieren. | `docs/runbooks/production-deployment.md` | Konkreter Restore-Nachweis für das Zielsystem ist zu erbringen. |
| Dependency-Registry wird als SQLite auf gemeinsamem Volume beschrieben. | `docs/runbooks/production-deployment.md` | Schreibkonkurrenz und Eignung für geplante Replikazahl prüfen; kein unbelegtes HA-Versprechen. |
| MIWG-Bericht belegt Referenzmodelle und interaktive Eingaben. | `docs/reviews/2026-09-06_MIWG_Conformance.md` | Kein Beweis jeder BPMN-Kombination oder einer vollständigen DMN-TCK-Abnahme. |
| UI-Abnahme umfasst lokale Browserprüfungen und ausgewählte reale Infrastrukturpfade. | `docs/reviews/2026-09-08_Studio-UI-Phase-7-Lokale-Gesamtabnahme.md` | Nach Dependency-Updates aktuellen Release erneut qualifizieren; Editor-Accessibility bleibt gesondert zu prüfen. |

## 3. Phasen und Reihenfolge

| Phase | Ergebnis | Priorität | Aufwand | Abhängigkeiten | Status |
|---|---|---|---|---|---|
| 0 | Zielumgebung, Betriebsziele und Nachweismatrix | Muss | S | keine | [x] |
| 1 | Korrekte OAuth2-Persistenz und Migrationspfade | Muss | M | 0 | [x] |
| 2 | Reproduzierbare lokale Release-Abnahme | Muss | M | 1 | [x] AcceptancePassed ab sauberem Checkout `3805236` (1005 Tests, 0 Fehler, 0 Skips) |
| 3 | Sicherheitsabnahme | Muss | L | 2 | [ ] |
| 4 | Wiederanlauf und Mehrreplikabetrieb | Muss | L | 2 | [ ] |
| 5 | Restore, Upgrade und Rollback | Muss | M–L | 4 | [ ] |
| 6 | Last, Dauerbetrieb und belastbare Kapazitätsgrenzen | Sollte; Muss für zugesagte Kapazität | L | 3–5 | [ ] |
| 7 | Konsistente Konformitäts- und Supportnachweise | Muss für Vollkonformitätszusage | L, nach Inventur nachschätzen | 2 | [ ] |
| 8 | Überwachter Pilot und Release-Freigabe | Muss | M plus Pilotlaufzeit | 3–7 | [ ] |

Phase 3 und 4 können nach Phase 2 parallel erfolgen; Phase 7 ebenfalls. Ein Abschlussdatum hängt von Befunden und Zielumgebung ab.

## 4. Konkrete Umsetzung und Abnahme

### Phase 0 – Produktionsprofil und Freigabekriterien

- [x] Tatsächliches Hosting, Betriebssystem, Datenbank-/Broker-Versionen, Replikazahl, Identity Provider und Secret-/Keyring-Speicherung festhalten. Kubernetes ist eine vorhandene Option, keine vorausgesetzte Nutzerentscheidung. (Versionen/Replikazahl erfasst; Hosting, IdP, Secret-Store bleiben sichtbar offen.)
- [x] Erwartete Last definieren: aktive und wartende Instanzen, Starts pro Sekunde, parallele Benutzer, Modellgrößen, Historienwachstum und Aufbewahrung. (Entschieden 2026-09-08: mittel – 1–10 Starts/s, 50–500 parallele Benutzer. Wartende Instanzen/Historienwachstum/Aufbewahrung noch zu quantifizieren → sichtbar offen.)
- [x] Verfügbarkeit, API-p95/p99, maximale Timer-Verzögerung, RPO (maximaler Datenverlust) und RTO (Wiederherstellungszeit) als messbare Zielwerte vereinbaren. Zahlen nicht aus lokalen Mikrobenchmarks ableiten. (Entschieden 2026-09-08: 99,5 %; p95 < 1 s; p99 < 3 s; RPO ≤ 15 min; RTO ≤ 4 h. Maximale Timer-Verzögerung noch offen – Vorschlag p95 ≤ 30 s.)
- [x] Vorhandene Tests und Berichte inventarisieren: Feature → öffentlicher Einstieg → Persistenz → Test → letzter geprüfter Commit → offene Grenze. (Nachweis-Inventur in `2026-09-08_Produktionsprofil.md`; Basis `e4b3949`+Fixes, aktueller `master` `296366e` noch nicht nachqualifiziert.)

Abnahme: Versioniertes Produktionsprofil mit konkreten Zielwerten; unbekannte Werte sichtbar offen. Zielabhängige Freigaben bleiben bis zur Festlegung offen, unabhängige technische Arbeiten können fortgesetzt werden.

Verifizierter Stand Phase 0 (2026-09-08): `docs/reviews/2026-09-08_Produktionsprofil.md` versioniert mit Commit-Basis `296366e`, konkreten Zielwerten (2 API + 1 Worker; mittlere Last; 99,5 %/p95 < 1 s/p99 < 3 s; RPO ≤ 15 min/RTO ≤ 4 h) und sichtbar offenen Punkten (Hosting/OS, IdP/Secret/TLS, maximale Timer-Verzögerung, Historienwachstum/Aufbewahrung, Backup-/Alarmverantwortliche). Nachweis-Inventur deckt Engine, REST, gRPC, SDK, CLI, Studio, DMN, CMMN, Connectors, MCP, OAuth2/Migration, Security, Betrieb und Last ab.

### Phase 1 – OAuth2 und Datenbankmigrationen

Betroffen: `OAuth2FlowStateCleanupService`, `OAuth2CredentialFlowService`, `BpmnDbContext` und die OAuth2-Migration unter `src/VertexBPMN.Infrastructure/`.

- [x] Fehler gegen echtes PostgreSQL reproduzieren: Upgrade-Test erwartet vor der Korrektur PostgreSQL `UndefinedFunction` beim produktiven Ablaufzeitvergleich.
- [x] PostgreSQL-UTC-Zeittypen und sichere Folgemigration für bereits installierte Datenbanken implementiert; ursprüngliche Migration unverändert.
- [x] Gespeicherte UTC-/Offsetwerte kontrolliert konvertiert; ungültige Werte brechen transaktional ab. SQLite-Upgrade geprüft.
- [x] Weitere zugesagte Produktionsprovider nach Profilentscheidung qualifizieren. (Phase-0-Profil 2026-09-08: PostgreSQL + RabbitMQ als Zielprofil; keine weiteren Produktionsprovider zugesagt.)
- [x] Tests für leere PostgreSQL-Neuinstallation und Upgrade mit bestehendem OAuth2-State ergänzen; UTC-Konvertierung unter Europe/Berlin sowie SQLite-Upgrade geprüft.
- [x] Gültigen Callback, abgelaufenen State, sequenzielles Replay, falschen Mandanten sowie Cleanup mit aktiven und abgelaufenen Einträgen gegen PostgreSQL geprüft. Aktive Einträge bleiben erhalten. Externer Token-Endpunkt simuliert; realer Identity Provider gehört zu Phase 3.

Abnahme: Migration und OAuth2-Lifecycle bestehen mit realem PostgreSQL; kein Typvergleichsfehler; keine Regression in zugesagten anderen Providern.

### Phase 2 – Lokale Release-Abnahme bündeln

Vorhandene Werkzeuge: `scripts/test-studio-e2e.ps1`, `scripts/test-studio-e2e.sh`, `scripts/verify-dependency-audit.sh`, `scripts/verify-coverage.sh`, `scripts/verify-reproducible-packages.sh` und die vorhandenen Acceptance-Skripte.

- [x] Lokalen Einstieg `scripts/test-production-readiness.ps1` mit nativen xUnit-Runner-Optionen ergänzt; Anleitung in `docs/runbooks/local-production-readiness.md`.
- [x] Release aus sauberem Checkout mit festgehaltenem Commit bauen; SDK-, Paket- und Infrastrukturversionen protokollieren. (Run `9aed5f0ecc07…` vom sauberen Checkout `3805236`, ohne `-AllowDirty`; SDK 10.0.302, Pakete in `packages.log`, PG/RabbitMQ-Versionen aus realen Verbindungen im externen XML.)
- [x] Kernsuite, UI-Verträge, reale GUI-E2E und externe PostgreSQL-/RabbitMQ-Verträge zusammengeführt. WSLC-/Existing-Anbindung implementiert; vollständiger Diagnoselauf gegen dedizierte WSLC-Dienste über Existing bestanden.
- [x] Pflichtfallliste und Report-Gates implementiert: null entdeckte Tests, fehlende/unvollständige Berichte, fehlende Pflichtmethoden und übersprungene Fälle führen zum Fehler. Sieben unabhängige Gate-Checks bestanden.
- [x] GUI-Fälle für Import, Bearbeiten, Export/Reimport, Validate, Deployment, gespeicherte Version erneut laden, User-Task-Completion sowie DMN/CMMN/Formulare ausgeführt. Getränke-Fixture eingeschlossen; Datei-Export und serverseitige Persistenz durch separate Assertions geprüft.
- [x] Ergebnisbericht mit Basiscommit, Dirty-Kennzeichen, dokumentiertem Aufruf, Dauer, bestanden/fehlgeschlagen/übersprungen, Logs und Browserartefakten erzeugt. Konsolenlogs werden redigiert; rohe XML-/Browserartefakte bleiben lokal und müssen vor Weitergabe geprüft werden. Große Artefakte bleiben außerhalb der Versionsverwaltung.
- [x] Nach Codeänderungen betroffene Nachweise erneuern; die finale Gesamtfreigabe gehört zum finalen Kandidaten. (Finale Abnahme ab sauberem Checkout des finalen Kandidaten `3805236` abgeschlossen.)

Abnahme: Ein lokaler Aufruf erzeugt einen nachvollziehbaren Bericht und korrekten Exitcode. Alle für das Profil erforderlichen Fälle laufen tatsächlich durch. Keine zusätzlichen GUI-Gates im CI.

Verifizierter Stand Phase 2 (2026-09-08): Finaler gebündelter Lauf `9aed5f0ecc074544b90945644edcfb10` vom **sauberen Checkout `3805236`** ohne `-AllowDirty` → **Status `AcceptancePassed`, Exitcode 0**. Gruppen: Kern **840/840**, externe PostgreSQL-/RabbitMQ-Verträge **7/7**, Browser-Verträge + lokale Opt-ins **61/61**, reale Studio-E2E **97/97** → **gesamt 1005, 0 Fehler, 0 Skips**. SDK 10.0.302; Artefakt-SHA-256 (API, Studio, Tests, UiTests-DLLs) im `summary.json`. Alle fünf isolierten E2E-Datenbanken nachweislich entfernt. Arbeitsbaum danach sauber (0 Änderungen). **Betriebshinweis (Reproduzierbarkeit):** Beim ersten Lauf flakten 4 Browser-Tests unter Volllast (Studio-Bootstrap-Timeouts/`ERR_CONNECTION_REFUSED`, `Failed to fetch`); isoliert und im Re-Run 61/61 grün → Harness-/Server-Start-Konkurrenz, kein Produktdefekt. Empfehlung: Browser-Stufe bei Verdacht wiederholen bzw. Server-Startfenster vergrößern. Diese lokale Abnahme ersetzt nicht Sicherheits-, Ausfall-, Restore-, Last- oder Zielumgebungsabnahme der späteren Phasen.

Verifizierter Stand 2026-09-08: Vollständiger gebündelter Lauf `0dc01e3d9f9e40ec9af05abbd0412dbd` erfolgreich mit **1.005 bestandenen Tests, 0 Fehlern, 0 Skips**: Kern 840, externe Verträge 7, Browser 61 und reale GUI-E2E 97. Status `DiagnosticPassed`, Basiscommit `e4b39490b06d01f0fe319368750ef5c1b009f097` mit uncommitteten Korrekturen; SDK 10.0.303, PostgreSQL 17.11, RabbitMQ 4.3.5. Alle fünf E2E-Datenbanken wurden anschließend gelöscht und ihre Abwesenheit verifiziert. Zehn unabhängige Berichtsskript-Prüfungen bestehen ebenfalls. Der Build hat 0 Fehler und 17 Warnungen. **Offen bleibt die finale Abnahme aus sauberem Checkout nach Commit der Korrekturen; keine Produktionsfreigabe.** Details und Nachweisgrenzen stehen in `docs/runbooks/local-production-readiness.md`.

### Phase 3 – Sicherheit

> **Stand 2026-09-08:** Abnahmebericht: `2026-09-08_Phase3_Sicherheitsabnahme.md`. Req 1/3/4/5 abgeschlossen; Req 2 (echter IdP) und Req 6 (unabhängiges Review) offen — externe Ressourcen, blockieren den Phasenabschluss.

- [x] Rollen-/Tenant-Matrix für REST, gRPC, Studio, SDK und verfügbare weitere öffentliche Adapter abgleichen; direkte Objektzugriffe über fremde IDs negativ testen. Matrix für 56 REST-Controller erstellt; T1–T4 (VertexJob, VertexVariable, SimulationScenario/Simulation, TaskIoSnapshot) behoben + 10 Negativtests; Rollen-Lücken S1–S6 als Mittel bewertet/dokumentiert.
- [ ] Produktionskonfiguration mit echtem Identity Provider prüfen: Login, Logout, abgelaufene Tokens, Rollenentzug und verweigerte Zugriffe. **Offen** — hängt an Phase-0-IdP-Entscheidung; externer Token-Endpunkt simuliert getestet.
- [x] OAuth2-State, Webhook-Authentifizierung und Replay, Secret-Rotation sowie Log-/Export-Redaktion prüfen. OAuth2-State+Rotation, Webhook-HMAC/Einmal-Secret, BpmnRedaction; M3 (Redaction nicht auf Export-Roh-XML) als Mittel dokumentiert; M1 (Connector-Redirect) inzwischen behoben (`AllowAutoRedirect=false`).
- [x] Script- und Connector-Grenzen testen: erlaubte Zieladressen, interne Adressbereiche, Redirects, Laufzeit-/Speicherlimits und Hostzugriffe entsprechend zugesagtem Sicherheitsmodell. Jint 2 s/8 MB, SSRF-Kern verifiziert; H1/H2 (Roslyn-RCE, Scripts-Enabled-Default) via `Runtime:Scripts:AllowCSharp`-Gate (default false) behoben; M1 (Connector-Redirect) behoben (`AllowAutoRedirect=false`); M2 (OAuth2-SSRF) + M5 (DNS-Rebinding/TOCTOU) behoben (Token-URL hinter SSRF-Guard; validierte-IP-Connect).
- [x] Dependency-Audit mit erreichbaren Quellen abschließen; NU1900 ist kein erfolgreicher Vulnerability-Nachweis. NuGet 19 Projekte (Quelle `api.nuget.org`) 0 vuln, npm Studio/FEEL 0 — alle Severities, direkt + transitiv.
- [ ] Für geschäftskritischen Einsatz unabhängiges Sicherheitsreview des finalen Kandidaten organisieren. **Offen** — organisatorisch, externer Review-Partner nötig.

Abnahme: Keine offenen ausnutzbaren kritischen oder hohen Befunde; wirksame Tenant-/Rollengrenzen und Secret-Behandlung sind durch positive und negative Fälle belegt.

### Phase 4 – Ausfall, Konkurrenz und Wiederanlauf

- [ ] API vor/nach Commit, während User-Task-Wait und während Timer-/Job-Verarbeitung kontrolliert beenden; Fortsetzung derselben Instanz prüfen.
- [ ] Broker nach Speicherung einer Outbox-Nachricht und vor/nach Bestätigung unterbrechen; Message-ID, Retry und idempotente Konsumenten prüfen.
- [ ] Datenbank zeitweise unerreichbar machen; Rückkehr zu einem konsistenten Zustand und begrenzte Retry-Last nachweisen.
- [ ] Zwei API-/Publisher-Replikate mit identischen Starts, gleichzeitiger Task-Completion, Lease-Ablauf und konkurrierenden Nachrichten betreiben.
- [ ] Externe Geschäftseffekte mit einem protokollierenden Testempfänger überprüfen. At-least-once-Zustellung ausdrücklich von idempotenter Geschäftsverarbeitung unterscheiden.
- [ ] Dependency-Registry unter konkurrierendem Zugriff prüfen. Falls SQLite das Zielprofil nicht erfüllt, persistenten zentralen Provider implementieren oder verifizierte Betriebsgrenze dokumentieren.

Abnahme: Keine verlorenen bestätigten Zustände; keine unkontrollierten doppelten Geschäftseffekte; nachvollziehbare Incidents und abgearbeitete Rückstände. Ein Containerneustart allein ist kein bestandener Test.

### Phase 5 – Wiederherstellung und Upgrade

- [ ] Konsistente Backups aller verwendeten Stores einschließlich Dependency-Registry und Data-Protection-Schlüsseln erstellen; Brokerzustand und Outbox-Replay im Recovery-Konzept berücksichtigen.
- [ ] Auf frischer isolierter Umgebung wiederherstellen. Modelle, Entscheidungen, Cases, Tasks, Timer und laufende Instanzen fachlich vergleichen und fortsetzen.
- [ ] Upgrade von der letzten freigegebenen Version mit realistischen Bestandsdaten prüfen; Migrationsfehler müssen den Rollout kontrolliert stoppen.
- [ ] Rückkehr zur alten Anwendung nur bei nachgewiesener Schemakompatibilität; andernfalls getesteten Backup-Restore verwenden.
- [ ] RPO/RTO messen und Runbook um tatsächlich ausgeführte Schritte sowie Ergebnisse ergänzen.

Abnahme: Ein zweiter Ausführender kann die Wiederherstellung anhand des Runbooks durchführen; gemessene Werte erfüllen Phase 0.

### Phase 6 – Last und Dauerbetrieb

- [ ] Szenarien für kurze Prozesse, langlebige Wait-States, Timer, parallele Gateways, DMN, Historienabfragen und gleichzeitige Studio-Sitzungen aufbauen.
- [ ] Last schrittweise steigern; p95/p99, Fehlerrate, Timer-Lag, Outbox-Alter, DB-Pool, Locks, CPU und Speicher erfassen.
- [ ] 24–72 Stunden als geplanten Dauerlauf mit Lastspitzen und kontrollierter Unterbrechung durchführen; genaue Dauer anhand Zielprofil festlegen.
- [ ] Nur gemessene Engpässe beheben, zum Beispiel Indizes, Paging, unbeschränkte Abfragen oder fehlerhafte Ressourcenfreigabe; relevante Tests wiederholen.
- [ ] Kapazitätsprofil mit Hardware, Datenvolumen, Replikazahl und Sättigungsgrenze veröffentlichen.

Abnahme: Betriebsziele unter vereinbarter Last erfüllt; kein unbeschränktes Speicher-/Verbindungswachstum und keine dauerhaft zunehmenden Rückstände.

### Phase 7 – Standardkonformität und ehrliche Supportaussagen

- [ ] BPMN-, DMN- und gegebenenfalls CMMN-Standardversionen sowie unterstützte öffentliche Ausführungspfade festhalten.
- [ ] Aktuelle MIWG- und DMN-TCK-Integration inventarisieren; Testsatzversion/Commit pinnen und vollständigen Ergebnisbericht erzeugen. Fehlende Runner ergänzen.
- [ ] Interaktive Modelle mit echten User-Task-Outputs, Entscheidungen und Events treiben. Ein erwarteter Wait-State ist korrekt, aber kein Nachweis des anschließenden Endzustands.
- [ ] Kombinationen risikobasiert prüfen: verschachtelte Scopes, Multi-Instance mit Boundary Events, Kompensation, konkurrierende Events und Wiederanlauf.
- [ ] Lokale Engine und persistente API auf dieselbe fachliche Semantik prüfen; fehlende Entscheidungen dürfen nicht unbemerkt als erfolgreiche Auswertung erscheinen.
- [ ] Supportmatrix, README und Konformitätsbericht mit konkreten Ergebnissen synchronisieren; widersprüchliche CI-Aussagen korrigieren.

Abnahme: Jede Zusage hat einen versionsgebundenen Nachweis. Keine Aussage „jede Kombination bewiesen“ aus einer endlichen Referenzsuite ableiten. Offene Fälle verhindern die entsprechende Vollkonformitätszusage, nicht automatisch jede enger abgegrenzte Produktfreigabe.

### Phase 8 – Monitoring, Pilot und Freigabe

- [ ] Alarme und Dashboards für API-Fehler, Job-/Timer-Lag, Incidents, Dead Letters, Outbox-Alter und Datenbankprobleme einrichten; Auslösung und Entwarnung testen.
- [ ] Zuständigkeit und Runbook für jeden Alarm festlegen; Prozess-ID, Tenant und Trace verknüpfen, ohne Secrets zu protokollieren.
- [ ] Begrenzten Pilotbetrieb mit realen, vereinbarten Geschäftsabläufen und einer vorab festgelegten Beobachtungsdauer durchführen.
- [ ] Finale Release-Artefakte auf Zielumgebung installieren; SDK und CLI aus den erzeugten Paketen in frischer Umgebung testen.
- [ ] Freigabebericht mit Commit, Artefakt-Hashes, Testergebnissen, Betriebsgrenzen, verbleibenden niedrigen Risiken und Rückfallverfahren abschließen.

Abnahme: Keine offenen Muss-Punkte des Zielprofils; Pilot erfüllt Betriebsziele; Wiederherstellung und Alarmierung funktionieren. Externe Bereitstellung und produktiver Rollout erfolgen nach Freigabe des konkreten Kandidaten.

## 5. Definition of Done und Pflege

Eine Phase erhält erst nach Umsetzung und überprüftem Nachweis ein Häkchen. Pro Phase werden geänderte Dateien, Testbefehle, Ergebnisbericht, Commit und offene Befunde verlinkt. Ein geplanter Test, übersprungener Pflichtfall oder nicht erreichbarer Auditdienst zählt nicht als bestanden.

Nächster Umsetzungsschritt: Phase 0 technisch inventarisieren und Phase 1 reproduzieren; anschließend Korrekturmigration und reale PostgreSQL-Regressionstests umsetzen. Dieses Dokument autorisiert noch keinen produktiven Ausfalltest oder Rollout.
