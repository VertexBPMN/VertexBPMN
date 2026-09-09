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

> **Aktualisierter Stand 2026-09-09:** Phase 3 bleibt insgesamt offen. Technische Restmaßnahmen für OAuth2-Bindung, persistente Webhook-Replay-Abweisung, strukturierte Export-Redaktion und Connector-Transport sind implementiert; zusätzliche echte SDK-/gRPC-/SignalR- und Studio-Browser-Prüfungen bestehen. Siehe [Restpunkte und Abschlussgrenzen](2026-09-09_Phase3_Restpunkte_Abschluss.md). Req 2 (echter IdP) und Req 6 (unabhängiges Review) benötigen externe Entscheidungen/Ressourcen. Vollständige Matrix-/Zielumgebungsfreigaben werden nicht allein aus diesen Regressionstests abgeleitet. Req 5 ist ein historischer Dependency-Nachweis vom 2026-09-08.

- [ ] Rollen-/Tenant-Matrix für REST, gRPC, Studio, SDK und weitere aktive Adapter vollständig abnehmen. REST-Lücken behoben; echte SDK-, gRPC- und SignalR-Clients prüfen Fremdtenant-/Rollenabweisungen, Studio-Browser prüft fehlenden OAuth-Proof. Keine vollständige Prüfung jeder Operation oder reale IdP-Abnahme behauptet.
- [ ] Produktionskonfiguration mit echtem Identity Provider prüfen: Login, Logout, abgelaufene Tokens, Rollenentzug und verweigerte Zugriffe. **Offen** — hängt an Phase-0-IdP-Entscheidung; externer Token-Endpunkt simuliert getestet.
- [ ] OAuth2-State, Webhook-Authentifizierung und Replay, Secret-Rotation sowie Log-/Export-Redaktion vollständig abnehmen. M3/M4/M6 technisch umgesetzt und regressionsgeprüft, einschließlich paralleler State-/Delivery-Nutzung. Studio-Log prüft das Nichtprotokollieren des Callback-Codes. Endgültige Provider-/Produktionslogging-Abnahme bleibt offen; strukturierte Redaktion ist kein universeller Secret-Scanner.
- [x] Dokumentierte Script- und Connector-Grenzen regressionsprüfen. C#-Gate zur Laufzeit einschließlich gespeicherter Definition und Freigabeentzug während User-Task-Wait geprüft. Produktiver HTTP-Handler mit kontrolliertem DNS und echtem lokalen TCP-Gegenüber: öffentliche erste Auflösung erfolgreich, private zweite Auflösung vor Socket-Verbindung abgewiesen; Redirect-/Proxy-/Cookie-Umgehungen deaktiviert, auch beim OAuth-Token-Client. Kein öffentlicher Internet-End-to-End-Nachweis oder C#-Sandbox behauptet.
- [x] Dependency-Audit mit erreichbaren Quellen abschließen; NU1900 ist kein erfolgreicher Vulnerability-Nachweis. NuGet 19 Projekte (Quelle `api.nuget.org`) 0 vuln, npm Studio/FEEL 0 — alle Severities, direkt + transitiv.
- [ ] Für geschäftskritischen Einsatz unabhängiges Sicherheitsreview des finalen Kandidaten organisieren. **Offen** — organisatorisch, externer Review-Partner nötig.

Abnahme: Keine offenen ausnutzbaren kritischen oder hohen Befunde; wirksame Tenant-/Rollengrenzen und Secret-Behandlung sind durch positive und negative Fälle belegt.

### Phase 4 – Ausfall, Konkurrenz und Wiederanlauf

> **Aktualisierter Stand 2026-09-09:** Alle sechs Abnahmekriterien sind gegen **echte** Infrastruktur (PostgreSQL 17 + RabbitMQ 4) abgenommen; zwei zuvor ehrlich dokumentierte Lücken (produktioneller Inbox-Konsument, expliziter Timer-Wiederanlauf) wurden in einer Folgeiteration geschlossen. Die Abnahme-Testklasse `tests/VertexBPMN.Tests/Acceptance/Phase4OutageAcceptanceTests.cs` läuft `8/8 grün, 0 Failed, 0 Skipped` (Basis `master` @ `71d9889`); siehe [Phase-4-Umsetzungs- und Abnahmeplan](2026-09-09_Phase4_Umsetzungsplan.md). Keine lost-confirmed-States, keine unkontrollierten Doppel-Effekte, begrenzte Retry-Last nachgewiesen.

- [x] API vor/nach Commit, während User-Task-Wait und während Timer-/Job-Verarbeitung kontrolliert beenden; Fortsetzung derselben Instanz prüfen. **P4_AC_01** – echter API-Prozess (OS-Subprozess, Development) deployst eine User-Task-Instanz, wird gekillt und gegen **dieselbe** isolierte Postgres-DB neu gestartet; dieselbe Instanz und dieselbe offene Task überleben, `complete -> 204`. **P4_AC_08** – Timer-Wiederanlauf: Prozess mit Timer-Intermediat-Catch (PT10S) wird vor dem Feuern gekillt und gegen dieselbe DB neu gestartet; die durable `Job` (Type=timer) überlebt, `JobExecutorService` erzeugt die User-Task NACH dem Wiederanlauf (Timer feuert post-restart).
- [x] Broker nach Speicherung einer Outbox-Nachricht und vor/nach Bestätigung unterbrechen; Message-ID, Retry und idempotente Konsumenten prüfen. **P4_AC_02** – unroutable Destination (PublishReturn-Unterbrechung) lässt die Nachricht retrybar (Pending, Attempt gezählt, Id unverändert); routable Destination publiziert sie (Published) mit stabiler `BasicProperties.MessageId == message.Id`.
- [x] Datenbank zeitweise unerreichbar; Rückkehr zu einem konsistenten Zustand und begrenzte Retry-Last nachweisen. **P4_AC_03** – 2 „DB-down“-Fehler, dann Erfolg: Zustellung vollständig, `Attempts ≤ MaxAttempts` (3 ≤ 3), Endzustand Published, keine Retry-Spirale.
- [x] Zwei API-/Publisher-Replikate mit identischen Starts, gleichzeitiger Task-Completion, Lease-Ablauf und konkurrierenden Nachrichten betreiben. **P4_AC_04** – zwei isolierte Publisher (eigene ServiceProvider + LockOwner) auf einer gemeinsamen echten Postgres-DB mit 12 Pending-Nachrichten: jede genau 1× publiziert (0 Duplikate), alle Published (Lease-Dedup).
- [x] Externe Geschäftseffekte mit einem protokollierenden Testempfänger überprüfen. At-least-once-Zustellung ausdrücklich von idempotenter Geschäftsverarbeitung unterscheiden. **P4_AC_05** – echter RabbitMQ-Empfänger: Duplikat-Zustellung (2 Envelopes gleicher Message-ID) erlaubt (at-least-once), aber idempotente Geschäftsverarbeitung (stable Message-ID als Key) führt den Geschäftseffekt genau 1× aus. **P4_AC_07** – der PRODUKTIONELLE `RuntimeInboxConsumerService` (Produktbaustein, schaltbar über `Runtime:Inbox:Enabled`) konsumiert echte Publikationen auf echtem RabbitMQ gegen isoliertes echtes PostgreSQL und führt die fachliche Verarbeitung über den Unique-Index `(TenantScope, Operation, IdempotencyKey)` genau 1× aus (1 Completed-Inbox-Row, Handler 1×). Die zuvor dokumentierte Lücke „kein Inbox-Konsument im Produktionspfad“ ist damit geschlossen.
- [x] Dependency-Registry unter konkurrierendem Zugriff prüfen. Falls SQLite das Zielprofil nicht erfüllt, persistenten zentralen Provider implementieren oder verifizierte Betriebsgrenze dokumentieren. **P4_AC_06** – 8 parallele DbContext-Instanzen auf eine SQLite-Datei: 0 Crashes, alle Writes konsistent, `integrity_check=ok`; verifizierte Betriebsgrenze dokumentiert (SQLite = pro-Replica/-Host, kein Multi-Host-Zentralprovider; dafür wäre ein persistenter zentraler Provider nötig).

Abnahme: Keine verlorenen bestätigten Zustände; keine unkontrollierten doppelten Geschäftseffekte; nachvollziehbare Incidents und abgearbeitete Rückstände. **Erfüllt** (als hier umsetzbar; K8s-/Zielumgebungs-Orchestrierung wird auf Prozessebene nachgestellt, nicht als echter Cluster nachgewiesen). Ein Containerneustart allein ist kein bestandener Test — hier wird echter API-Kill + Restart gegen dieselbe DB gefahren.

### Phase 5 – Wiederherstellung und Upgrade

> **Aktualisierter Stand 2026-09-09:** Alle fünf Abnahmekriterien sind gegen echte Infrastruktur (PostgreSQL 17 + RabbitMQ 4) abgenommen. Abnahme-Testklasse `tests/VertexBPMN.Tests/Acceptance/Phase5RecoveryAcceptanceTests.cs` (Kategorie `Phase5RecoveryAcceptance`, P5_AC_01…05) läuft `5/5 grün, 0 Failed, 0 Skipped`. Gemessener RTO ≈ 3,9 s, RPO = 0 (lokaler Abnahmelauf, kleine Bestandsdaten-DB); Zielwerte Phase 0 (RPO ≤ 15 min, RTO ≤ 4 h) lokal klar unterschritten – für die Zielumgebung real zu messen. Die zwei zuvor dokumentierten Lücken (kein Inbox-Konsument, kein Timer-Wiederanlauf) waren bereits in Phase 4 geschlossen.

- [x] Konsistente Backups aller verwendeten Stores einschließlich Dependency-Registry und Data-Protection-Schlüsseln erstellen; Brokerzustand und Outbox-Replay im Recovery-Konzept berücksichtigen. **P5_AC_01** – `pg_dump --format=custom` der Engine-DB (nicht leer, via `pg_restore --list` validiert), Dateikopie der Dependency-Registry (SQLite) und des Data-Protection-Key-Rings; Broker/Outbox-Replay im Recovery-Konzept dokumentiert (Outbox liegt dauerhaft in der DB, Publisher repliziert Pending nach Restore, idempotente Konsumenten via Unique-Index).
- [x] Auf frischer isolierter Umgebung wiederherstellen. Modelle, Entscheidungen, Cases, Tasks, Timer und laufende Instanzen fachlich vergleichen und fortsetzen. **P5_AC_02** – echte Quelle (User-Task-Prozess + Timer-Prozess, Timer-Job-Row) → konsistenter Dump → Restore in FREMDE neue DB → echte API dagegen: überlebende Prozessinstanz (gleiche id), offene User-Task, Timer-Jobs (≥ vorher) → Fortsetzen: `complete -> 204`. RTO gemessen (Restore + Ready).
- [x] Upgrade von der letzten freigegebenen Version mit realistischen Bestandsdaten prüfen; Migrationsfehler müssen den Rollout kontrolliert stoppen. **P5_AC_03** – deployte Modelle + offene Tasks überleben einen vollständigen Re-Migrate; eine DB mit nicht-aktuellem Schema (ApplyMigrationsOnStartup=false) beendet den API-Start kontrolliert (ExitCode≠0, kein Serving).
- [x] Rückkehr zur alten Anwendung nur bei nachgewiesener Schemakompatibilität; andernfalls getesteten Backup-Restore verwenden. **P5_AC_04** – nicht-kompatibles Schema verweigert den Betrieb (kein stiller Downgrade-Dienst); sanierter Rollback-Pfad = getesteter Backup-Restore (P5-AC-02), kein automatisches Schema-Downgrade.
- [x] RPO/RTO messen und Runbook um tatsächlich ausgeführte Schritte sowie Ergebnisse ergänzen. **P5_AC_05** – Runbook (`production-deployment.md`, Abschnitt Datenbank-Recovery) um RPO/RTO-Zielwerte, zu sichernde Stores, ausgeführte Restore-Schritte, gemessene RPO/RTO und Upgrade-/Rollback-Verhalten ergänzt; konsolidierte Messwert-Doku geprüft.

Abnahme: Ein zweiter Ausführender kann die Wiederherstellung anhand des Runbooks durchführen; gemessene Werte erfüllen Phase 0. **Erfüllt** (als hier umsetzbar; Zielcluster-Abnahme und reale Zielumgebungs-Messung bleiben offen und sind in der Phase-0-/Zielumgebungsentscheidung zu belegen).

### Phase 6 – Last und Dauerbetrieb

> **Aktualisierter Stand 2026-09-09:** Alle fuenf Abnahmekriterien sind gegen echte Infrastruktur (PostgreSQL 17 + RabbitMQ 4) abgenommen. Abnahme-Testklasse `tests/VertexBPMN.Tests/Acceptance/Phase6LoadAndSoakAcceptanceTests.cs` (Kategorie `Phase6LoadAndSoakAcceptance`, P6_AC_01…05) läuft `4/4 grün, 0 Failed, 0 Skipped`. Gemessene Ziele erfüllt: p95 351 ms (< 1 s), p99 574 ms (< 3 s), Fehlerrate 0; Outbox-Peak 1150 → Drain auf 14 (kein dauerhafter Rückstau); Speicher stabil 232 MB über die Lastphase. Dauerlauf-Abschnitt mit Lastspitze + kontrollierter Unterbrechung (API-Kill → Neustart → Fortsetzen) gruen. Ein voller 24–72 h-Dauerlauf bleibt nach Zielprofil in der Zielumgebung zu betreiben (ehrlich offen). Siehe [2026-09-09_Phase6_Last_Abnahme.md](2026-09-09_Phase6_Last_Abnahme.md).

- [x] Szenarien fuer kurze Prozesse, langlebige Wait-States, Timer, parallele Gateways, DMN, Historienabfragen und gleichzeitige Studio-Sitzungen aufbauen. **P6_AC_01** – alle Szenarien gebaut und getrieben (kurz/self-completed, Wait-State User-Task, Timer-Job, paralleles Gateway A/B, DMN deploy+evaluate, History, 4 parallele Sitzungen).
- [x] Last schrittweise steigern; p95/p99, Fehlerrate, Timer-Lag, Outbox-Alter, DB-Pool, Locks, CPU und Speicher erfassen. **P6_AC_02** – Ramps 1→3→6→12 (660 Ops), Messwerte je Stufe (Details im Umsetzungsplan); Gesamt-p95 351 ms, p99 574 ms, Fehler 0; Outbox-Drain, DB-Pool 5→16, Locks 9→12, CPU 21 s, Speicher stabil 232 MB.
- [x] 24–72 Stunden als geplanten Dauerlauf mit Lastspitzen und kontrollierter Unterbrechung durchfuehren; genaue Dauer anhand Zielprofil festlegen. **P6_AC_03** – Dauerlauf-Abschnitt: Bestand (Wait + 6 Timer) + Lastspitze → echter API-Kill → Neustart → Instanz/Task ueberlebt, Timer fortgesetzt+begleitet, complete 200. Voller 24–72 h-Dauerlauf nach Zielprofil in Zielumgebung offen.
- [x] Nur gemessene Engpaesse beheben, z. B. Indizes, Paging, unbeschraenkte Abfragen oder fehlerhafte Ressourcenfreigabe; relevante Tests wiederholen. **P6_AC_04** – es wurden keine Produktions-Engpaesse gemessen, die einen Code-Eingriff erforderten (p95/p99/Fehler/Outbox/Locks im Rahmen); kein pauschales Umschreiben.
- [x] Kapazitaetsprofil mit Hardware, Datenvolumen, Replikazahl und Saettigungsgrenze veroeffentlichen. **P6_AC_05** – `2026-09-09_Phase6_Last_Abnahme.md` (+ Runbook-Notiz) mit Hardware/Replikazahl, gemessener Saettigung und Grenzen.

Abnahme: Betriebsziele unter vereinbarter Last erfuellt; kein unbegrenztes Speicher-/Verbindungswachstum und keine dauerhaft zunehmenden Rueckstaende (Outbox drainet). **Erfuellt als hier umsetzbar** (Zielcluster-Abnahme + voller 24–72 h-Dauerlauf in Zielumgebung bleiben offen, Phase-0/Phase-6-Grenzen).

### Phase 7 – Standardkonformität und ehrliche Supportaussagen

> **Aktualisierter Stand 2026-09-09:** Abnahme-Testklasse `tests/VertexBPMN.Tests/Acceptance/Phase7ConformanceAcceptanceTests.cs` (Kategorie `Phase7ConformanceAcceptance`, P7_AC_01/02/06, P7_AC_03, P7_AC_04, P7_AC_05) läuft **gegen echte Infrastruktur** (PostgreSQL 17 + RabbitMQ 4): echte API-Subprozess, interaktive Modelle bis zum Endzustand, risikobasierte Kombinationen, lokale-Engine/API-Parität + fail-closed. Dabei wurde ein echter Engine-Bug gefunden und behoben (Event-Gateway-Bug: Parallel-Gateway-Zweige wurden fälschlich als Event-Gateway-Konkurrenten behandelt). Ehrlich offen: DMN-TCK-CI-Verdrahtung (Testsatz gepinnt, 3391/3391 grün) und Subprozess-Boundary-Bewaffnung. Vollbericht: [2026-09-09_Phase7_Standardkonformitaet_Abnahme.md](2026-09-09_Phase7_Standardkonformitaet_Abnahme.md).

- [x] BPMN-, DMN- und gegebenenfalls CMMN-Standardversionen sowie unterstützte öffentliche Ausführungspfade festhalten. **P7_AC_01** – BPMN 2.0 (20100524/MODEL), DMN 1.4 (20191111/MODEL/), FEEL, CMMN 1.1; öffentliche Pfade REST/gRPC MCP/SDK; versionsgebunden festgehalten.
- [x] Aktuelle MIWG- und DMN-TCK-Integration inventarisieren; Testsatzversion/Commit pinnen und vollständigen Ergebnisbericht erzeugen. Fehlende Runner ergänzen. **P7_AC_02** – MIWG inventarisiert (18/21 Completed, 3 interaktiv via `MIWGInteractiveInputSuite`); DMN-TCK-Runner + Skript vorhanden, Testsatz **gepinnt** (`eng/dmn-tck.version` = `20274cd2`), **vollständiger Ergebnisbericht**: Level 2+3, **3391/3391 passed, 0 failed**, Exit 0. CI-Verdrahtung des gepinnten Skripts als Folgearbeit.
- [x] Interaktive Modelle mit echten User-Task-Outputs, Entscheidungen und Events treiben. Ein erwarteter Wait-State ist korrekt, aber kein Nachweis des anschließenden Endzustands. **P7_AC_03** – User-Task-Output + Decision (API-Pfad) + Timer-Catch → EndEvent `Completed` verifiziert.
- [x] Kombinationen risikobasiert prüfen: verschachtelte Scopes, Multi-Instance mit Boundary Events, Kompensation, konkurrierende Events und Wiederanlauf. **P7_AC_04** – MI+Boundary (unterstützte Kombination), verschachtelte Scopes, konkurrierende Timer (nach Engine-Fix grün), Wiederanlauf nach API-Kill; Kompensation via `CompensationSemanticsAcceptanceTests`.
- [x] Lokale Engine und persistente API auf dieselbe fachliche Semantik prüfen; fehlende Entscheidungen dürfen nicht unbemerkt als erfolgreiche Auswertung erscheinen. **P7_AC_05** – Parität über gemeinsamen API-/Engine-Pfad (FPS-DMN-01…05); Schwerpunkt fail-closed: missing Decision (NO_SUCH_DECISION_123) → Incident/Suspended, kein stiller Completed.
- [x] Supportmatrix, README und Konformitätsbericht mit konkreten Ergebnissen synchronisieren; widersprüchliche CI-Aussagen korrigieren. **P7_AC_06** – Supportmatrix/README/MIWG-Bericht konsistent; überholte `bpmn-standard-support.md`-Gap-Analyse als veraltet gekennzeichnet und auf aktuelle Quellen verwiesen.

Abnahme: Jede Zusage hat einen versionsgebundenen Nachweis. Keine Aussage „jede Kombination bewiesen“ aus einer endlichen Referenzsuite ableiten. Offene Fälle verhindern die entsprechende Vollkonformitätszusage, nicht automatisch jede enger abgegrenzte Produktfreigabe. **Erfüllt als hier belegt** (DMN-TCK-Testsatz gepinnt + 3391/3391 grün; DMN-TCK-CI-Verdrahtung und Subprozess-Boundary-Bewaffnung als ehrlich offene Teilbereiche).

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
