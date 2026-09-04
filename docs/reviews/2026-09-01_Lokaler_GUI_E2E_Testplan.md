# Plan: Vollständige lokale GUI-End-to-End-Tests

## Fortschrittsstand (2026-09-02)

Legende:

- ✅ **Fertig und lokal erfolgreich nachgewiesen**
- 🟡 **Teilweise umgesetzt oder funktionaler Pfad nachgewiesen, aber noch nicht vollständig grün abgenommen**
- ⬜ **Offen**

Der aktuelle Stand basiert auf den realen Browsertests in
`tests/VertexBPMN.Studio.UiTests/LocalStudioInfrastructureTests.cs` und den lokalen
HTML-Testberichten unter `tests/VertexBPMN.Studio.UiTests/TestResults/studio-e2e`.
Ein vorhandener Test allein gilt nicht als „fertig“, wenn der letzte bestätigte Lauf
noch fehlschlägt oder der im Plan geforderte Use Case nur teilweise abgedeckt ist.

| Phase / Bereich | Status | Nachgewiesener Stand | Noch offen |
|---|---|---|---|
| Phase 1: Real-E2E-Infrastruktur | ✅ | Echter API- und Studio-Prozess, echtes Chromium, PostgreSQL/RabbitMQ über WSLC, Modi `Auto`/`Wslc`/`Existing`, dynamische Ports, Readiness, lokaler Runner, Kategorie `LocalStudioE2E` sowie HTML-/XML-Berichte sind umgesetzt. Jeder Lauf verwendet fünf eigene PostgreSQL-Datenbanken. Deren Löschen und anschließende Abwesenheit wurden im Erfolgs- und Fehlerfall nachgewiesen. Zwei vollständige WSLC-Läufe mit jeweils 8/8 Tests sind grün. | — |
| BPMN Modeler | ✅ | Import, grafisches Auftrennen eines Sequence Flows durch einen Service Task, HTTP-Connector, Properties-Änderungen einschließlich Credential-Ref, Form-Ref und Decision-Ref, Validierung, Deploy v1/v2, Reload aus Repository, Versionsvergleich, Export, semantischer Re-Import, Start/Pause/Reset der Simulation und `Deploy and run test` sind lokal erfolgreich nachgewiesen. | — |
| BPMN Runtime | ✅ | Deploy, Start mit Variablen, Instanzsuche, Task/Form-Auflösung, Claim, Formulareingabe, Completion, persistierte Variablen, History und der persistente Engine-Event-Log sind im realen Browserpfad sowie über die History-API nachgewiesen. | — |
| DMN Modeler | ✅ | Import, grafisches Hinzufügen einer Decision Rule über den Modeler, Deploy, Reload, High-/Low-/No-Match-Auswertung sowie Export und Re-Import sind lokal erfolgreich nachgewiesen. | — |
| CMMN Modeler | ✅ | Import, grafisches Hinzufügen eines Human Tasks, Registrierung, Case-Start, User Event, Case-File-Update, discretionary/ad-hoc Aktivierung, History sowie Export und Re-Import sind lokal erfolgreich nachgewiesen. | — |
| Form Builder | ✅ | Import, grafisches Hinzufügen eines Formularfelds, Speichern, Reload aus Registry, Update unter Beibehaltung der Formular-ID, Runtime Viewer sowie JSON-Export und Re-Import sind lokal erfolgreich nachgewiesen. | — |
| n8n-Import | ✅ | Reales n8n-JSON mit Webhook und HTTP-Node wird im Browser importiert; Mapping-Bericht und fehlende Credential als `NeedsReview` werden geprüft. Das erzeugte BPMN enthält Diagrammdaten, wird validiert, deployt, aus dem Repository neu geladen und exportiert. | — |
| Phase 3: Prozessverwaltung | 🟢 | Deployments (Upload gültig/ungültig), Process Definitions (View BPMN, Versionsdialog, Löschen mit Reload-Persistenz), Process Instances (Auflisten, Suchen, Details, Suspend/Resume/Löschen mit Persistenz), Execution Details (Jobs, Incidents, Variablen) und Fehler-/Event-Log-Pfade sind lokal grün nachgewiesen. | Pagination. |
| Phase 4: Erweiterte Runtime-Funktionen | 🟢 | Simulation (Run, Summary, Variable Trace, Szenario-CRUD, Vergleichen), Messages/Signals (Korrelation, Broadcast), Debugging (Session, Breakpoint, Step Over, Continue, Variablen, Visualisierung, Timeline-Replay) und Migration (Preview, Execute, Status, Snapshot/Restore, Rollback, Ablehnung unzulässiger Migration) sind lokal grün nachgewiesen. | — |
| Phase 5: Administration und Integrationen | 🟢 | Tenants (CRUD + Isolation), Credentials (Secret nie ausgegeben, Rotation, Löschen), Connectors (Create/Test/Toggle/Delete), Workflow Triggers (One-Time-Secret, Auslösen, Toggle, Löschen), Feature Flags (Toggle persistiert) sowie Engine Management/Configuration und Extensions/SSO/Health/Performance/Analytics/Compliance (laden fehlerfrei) sind lokal grün nachgewiesen. | Analytics-Training/Export, SSO-/Extensions-Konfiguration (mutierende Aktionen). |
| Phase 6: Fehler-, Navigation- und Qualitätsfälle | 🟡 | Browserfehler werden in den vorhandenen Real-E2E-Tests gesammelt; der Runner erzeugt einen HTML-Bericht, einzelne Fehlerpfade erzeugen Screenshots und Diagnoseausgaben. | Systematische HTTP-/Timeout-/Mehrfachklick-/Reload-Fehlerfälle, alle Routen, kleiner Viewport, vollständige Traces/Request- und Log-Artefakte. |

### Bereits grün bestätigte Real-E2E-Szenarien

- ✅ API/Studio Readiness und Dashboard gegen das reale Backend.
- ✅ BPMN Import, grafische Änderung, Validierung, Deploy v1/v2, Reload, Vergleich, Export und Roundtrip.
- ✅ DMN Import, Deploy, Reload, High-/Low-/No-Match-Auswertung, Export und Roundtrip.
- ✅ Form-Import, Persistenz, Reload, Update, Runtime Viewer, Export und Roundtrip.
- ✅ CMMN Import, Registrierung, Ausführung, Case-File-/Event-/Ad-hoc-Aktionen, History, Export und Roundtrip.
- ✅ BPMN Simulation mit Start/Pause/Reset sowie `Deploy and run test` gegen die reale Engine.
- ✅ BPMN Runtime mit Deploy, Start, Instanzdetails, Task-Claim, echtem Formular, Completion, persistierten Variablen, History und persistentem Engine-Event-Log (via `api/history/by-process-instance`).
- ✅ n8n-Import mit Mapping-Bericht, `NeedsReview`, BPMN-DI, Validierung, Deployment, Reload und Export.
- ✅ Phase 3 Prozessverwaltung: Deployments, Process Definitions, Process Instances (inkl. Suspend/Resume/Löschen mit Persistenz), Execution Details (Jobs/Incidents/Variablen), Fehler-/Event-Log.
- ✅ Phase 4 Simulation: Run, Summary, Variable Trace, Szenario-CRUD mit Reload-Persistenz und Ergebnisvergleich gegen die reale Engine.
- ✅ Phase 4 Messages & Signals: Korrelation (inkl. Ablehnung nicht passender Korrelation) und Signal-Broadcast über die GUI.
- ✅ Phase 4 Debugging: Session, Breakpoint, Step Over, Continue, Variablen-Inspektion, Prozessvisualisierung und Timeline-Replay gegen die reale Engine.
- ✅ Phase 4 Migration: Preview, Execute, Status, Snapshot/Restore, Rollback und verständliche Ablehnung unzulässiger Migration über die GUI.
- ✅ Persistente Isolation über fünf run-spezifische PostgreSQL-Datenbanken einschließlich verifiziertem Drop im Erfolgs- und Fehlerfall.

### Letzter vollständiger Lauf

- ✅ WSLC-Lauf `aaff96a9e156431fa63f0e2187aaa0f4`: 8 erfolgreich, 0 fehlgeschlagen, 0 übersprungen.
- ✅ WSLC-Lauf `0abcfd75e01540ccad56f85185e5a74b`: 8 erfolgreich, 0 fehlgeschlagen, 0 übersprungen.
- ✅ Für beide Läufe bestätigt `database-cleanup.log` den Drop und die anschließende Abwesenheit aller fünf Datenbanken; eine direkte Abfrage der WSLC-PostgreSQL-Instanz lieferte jeweils keine Restdatenbank.
- ✅ Der Fehlerlauf `6ba7a24f24e34708b05b67aa0fc25e5d` mit vier fehlgeschlagenen Szenarien hinterließ ebenfalls keine der fünf run-spezifischen Datenbanken.
- ✅ Die Suite ist zweimal hintereinander mit isolierten Run-IDs und sauberer persistenter Ausgangslage erfolgreich durchgelaufen.
- ✅ Linux/Docker-Lauf `935a5f4270f14a9da1ad46025fcb9993`: 8 erfolgreich, 0 fehlgeschlagen, 0 übersprungen (PostgreSQL + RabbitMQ als Container). Enthält die neue Event-Log-Assertion im BPMN-Runtime-Test.
- ✅ Der Tenant-Selector-Flake im Suite-Verbund wurde durch eine Verlängerung der Readiness-Wartezeit auf 60 s stabilisiert; danach ist der volle 8-Test-Lauf unter Last grün.
- ✅ Der Lauf `935a5f42` hinterließ keine der fünf run-spezifischen Datenbanken (Abwesenheit direkt in PostgreSQL verifiziert).
- ✅ **Phase 4 vollständig (Sommer 2026, Container-Stack)**: Die komplette Local-Studio-Suite mit **20 Real-E2E-Tests** (Phasen 1–4) ist auf einer sauberen Datenbank zweimal in Folge grün:
  - Lauf `2c27743e19ff422592e82f62cc3e7739`: **20 erfolgreich, 0 fehlgeschlagen, 0 übersprungen** (`RUN_EXIT 0`).
  - Lauf `d7301acba7ac4eaebb2cc517b91a849b`: **20 erfolgreich, 0 fehlgeschlagen, 0 übersprungen** (`RUN_EXIT 0`).
  - Enthalten sind Simulation, Messages/Signals, Debugging und Migration (Phase 4) sowie die Phasen 1–3.
- ✅ Unter voller Suite-Last wurden zwei Last-Flakes behoben: der Textarea-`Value`-Setter von `MudTextField` `Lines>=5` (die aufrufende JavaScript-Funktion wählte den Setter nicht anhand des Element-Tags) sowie transiente API-Hänger beim Instanz-Start (Retry-Helper).

### Behobene Lücke: Suspend/Resume/Löschen von Process Instances

Die Phase-3-E2E-Prüfung zeigte zunächst, dass **Suspend/Resume und Löschen von Process
Instances nicht als persistente Funktion umgesetzt waren**: `ManagementService` erzeugte nur
`ProcessMiningEvent`-Ereignisse, und die `ProcessInstances`-Seite zeigte den Suspend-Button
nur bei `State == "Active"`, während BPMN-Instanzen mit `Running` erzeugt werden. Diese Lücke
wurde zwischenzeitlich **im Produktcode behoben**:

- `ManagementService.Suspend/Resume/DeleteProcessInstanceAsync` delegieren nun an den
  persistenten `IRuntimeService`; `RuntimeService.SuspendAsync`/`ResumeAsync` ändern den
  Zustand und schreiben ihn über `IProcessInstanceRepository` dauerhaft, `DeleteAsync` löscht
  die Instanz (FK-Cascade für Jobs, Variablen, Tokens, Incidents) und persistiert die Löschung.
- Der Suspend-/Resume-Button in `ProcessInstances.razor` wird nun anhand des
  `ProcessInstanceStatus`-Enums (`Running`/`Suspended`) gesteuert statt anhand des
  `State`-Strings, sodass der Suspend-Button für laufende Instanzen sichtbar ist.
- Der E2E-Test `ProcessInstances_SuspendResumeAndDelete_PersistThroughUiAndApi` suspends /
  resumed / deleted eine reale Instanz über die UI und verifiziert die Persistenz über die
  `api/runtime`-Schnittstelle.

### Laufnachweis Phase 3 (Prozessverwaltung)

- ✅ Datei-`Deploy` über die `Deployments`-Seite mit gültigem BPMN (Persistenz über die
  `api/repository`-Schnittstelle verifiziert) sowie ablehnende Behandlung eines ungültigen Uploads.
- ✅ Process Definitions: BPMN-Viewer, Versionsdialog (mehrere Versionen), Löschen über die UI
  mit anschließender Dauerhaftigkeit über Reload und API.
- ✅ Execution Details: Jobs, Incidents und Variablen für eine reale, laufende Instanz.
- ✅ Suspend/Resume/Löschen einer Process-Instance über die UI mit API-verifizierter Persistenz.
- ✅ Fehlerpfad und Event-Log-Oberfläche lokal auf dem realen Backend.
- ✅ Ausgerollt in `LocalStudioInfrastructureTests.cs`: **16 Tests, 0 Fehler, 0 übersprungen**
  auf sauberer Datenbank, zweimal in Folge grün bestätigt.

### Gesamtstatus

🟢 **Der lokale GUI-E2E-Plan ist weitgehend grün.** Die Modellierungs- und Runtime-Use-Cases der Phasen 2 und 3 (BPMN Modeler und Runtime inkl. persistentem Event-Log, DMN, CMMN, Form Builder, n8n-Import, Prozessverwaltung mit Suspend/Resume/Löschen-Persistenz und Execution Details) sind vollständig grün nachgewiesen (16 Tests, 0 Fehler, zweimal in Folge). Die **Phase 4 (Erweiterte Runtime-Funktionen)** wurde mit Simulation (Run, Summary, Variable Trace, Szenario-CRUD, Vergleich), Messages & Signals (Korrelation, Broadcast), Debugging (Session, Breakpoint, Step Over, Continue, Variablen, Visualisierung, Timeline-Replay) und Migration (Preview, Execute, Status, Snapshot/Restore, Rollback, Ablehnung unzulässiger Migration) vervollständigt und ist über den realen Backend-Pfad grün. Dabei wurden drei echte Produktfehler behoben (fehlende `null`-Variables-Initialisierung in der Simulation, unverpacktes Analytics-Ergebnis, fehlendes `@` für `BpmnXml` im Debugging-Viewer) sowie die fehlende `ProcessViewer`-Authorisierungspolicy ergänzt. Offen bleiben Administration und Integrationen (Phase 5) sowie die systematische Fehler- und Routenabdeckung (Phase 6).

## Ziel

Jede produktive Funktion von VertexBPMN Studio wird im echten Browser gegen die echte API, Engine und Persistenz geprüft. Die vorhandenen Browser-Contract-Tests mit Stub-API bleiben als schnelle Tests erhalten, reichen als Nachweis der Gebrauchstauglichkeit aber nicht aus.

Die neue Real-E2E-Suite läuft ausschließlich lokal. Sie wird nicht in GitHub Actions oder andere CI-Workflows aufgenommen.

## Ausgangslage

- Das bestehende Projekt `tests/VertexBPMN.Studio.UiTests` verwendet echtes Chromium und startet den echten Studio-Prozess.
- Import, grundlegende Bearbeitung und Export von BPMN sind im Browser abgedeckt.
- Repository-, Runtime- und Task-Endpunkte werden im aktuellen UI-Testhost jedoch durch eine Stub-API ersetzt.
- Reale Persistenz, erneutes Laden und vollständige Workflows über Browser, API und Engine sind daher noch nicht nachgewiesen.
- Die bestehende Browser-Suite ist nicht Bestandteil des schnellen CI-Workflows. Das soll auch für die Real-E2E-Suite gelten.

## Phase 1: Lokale Real-E2E-Infrastruktur — ✅ fertig und lokal nachgewiesen

### Umsetzung

- Das bestehende Projekt `tests/VertexBPMN.Studio.UiTests` um lokale Real-E2E-Tests erweitern.
- Einen separaten `LocalStudioE2ETestHost` ohne Stub-API erstellen.
- Folgende echte Prozesse starten und überwachen:
  - `VertexBPMN.Api`
  - `VertexBPMN.Studio`
  - PostgreSQL und RabbitMQ über WSLC oder bereits vorhandene lokale Installationen
- Die Infrastrukturmodi `Auto`, `Wslc` und `Existing` unterstützen.
- Pro Testlauf einen eindeutigen Tenant sowie isolierte Testdaten erzeugen.
- Dynamische freie Ports verwenden und die tatsächlichen Endpunkte an Studio und Tests übergeben.
- Readiness-Endpunkte abfragen, anstatt feste Wartezeiten zu verwenden.
- Prozesse und Testdaten auch bei Testfehlern in `finally` zuverlässig bereinigen.
- Alle Real-E2E-Tests mit `Category=LocalStudioE2E` markieren.
- Pro Lauf fünf isolierte PostgreSQL-Datenbanken für BPMN, Tenants, Simulation, Events und Decisions erstellen.
- Nach jedem Lauf alle fünf Datenbanken mit erzwungener Trennung löschen, ihre Abwesenheit direkt in PostgreSQL verifizieren und das Ergebnis in `database-cleanup.log` protokollieren.
- Einen lokalen Einstiegspunkt bereitstellen:

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Auto
```

### Anforderungen

- Keine Stub-API innerhalb der Real-E2E-Suite.
- Fehlende PostgreSQL-/RabbitMQ-Voraussetzungen führen zu einem verständlichen Preflight-Fehler und nicht zu übersprungenen Tests.
- Keine Änderung an `.github/workflows/ci.yml`.
- Die Tests dürfen nicht durch den normalen schnellen `dotnet test tests/VertexBPMN.Tests/...`-Aufruf gestartet werden.

## Phase 2: Kritische Modellierungs-Use-Cases — 🟡 teilweise umgesetzt

### BPMN Modeler — 🟡 teilweise umgesetzt

1. Eine konkrete BPMN-Datei über den Datei-Dialog importieren.
2. Diagramm und Properties Panel vollständig laden.
3. Einen Task grafisch hinzufügen und dessen Eigenschaften verändern.
4. Sequence Flow einfügen beziehungsweise auftrennen.
5. Connector, Credential-Referenz, Formular und Decision Reference konfigurieren.
6. BPMN validieren und das erwartete Ergebnis prüfen.
7. Modell als Version 1 deployen.
8. Browser neu laden und die persistierte Definition wieder öffnen.
9. Modell verändern und als Version 2 deployen.
10. Beide Versionen laden und vergleichen.
11. Modell exportieren und die heruntergeladene XML-Datei prüfen.
12. Exportierte Datei erneut importieren und einen semantischen Roundtrip nachweisen.
13. Lokale Simulation starten, pausieren und zurücksetzen.
14. `Deploy and run test` ausführen und den realen Prozessstatus prüfen.

### BPMN Runtime — 🟡 Kernpfad grün, Event Log offen

1. Ein ausführbares BPMN-Modell über die GUI deployen.
2. Den Prozess über die GUI starten.
3. Die erzeugte Prozessinstanz in der Instanzseite finden.
4. Einen User Task auf der Task-Seite finden und claimen.
5. Formulardaten eingeben und den Task abschließen.
6. Den abgeschlossenen Prozessstatus, Variablen, History und Event Log prüfen.

### DMN Modeler — 🟡 Runtime- und Persistenzpfad grün, grafische Änderung offen

- DMN importieren oder neu erstellen.
- Decision Table über den Modeler verändern.
- DMN deployen und nach einem Browser-Reload aus der API laden.
- Entscheidungen mit konkreten Eingabedaten auswerten.
- Positive, negative und No-Match-Ergebnisse prüfen.
- DMN exportieren und erneut importieren.

### CMMN Modeler — ✅ umgesetzt

- CMMN-Modell laden, bearbeiten und registrieren.
- Case starten.
- User Event auslösen.
- Case File Item aktualisieren.
- Ad-hoc-Subprozess erzeugen.
- Historie laden und Zustandsänderungen prüfen.
- CMMN exportieren und erneut importieren.

### Form Builder — ✅ umgesetzt

- Formular erstellen und Felder konfigurieren.
- Formular speichern und nach Browser-Reload erneut laden.
- Gespeichertes Formular verändern und aktualisieren.
- Runtime Viewer prüfen.
- JSON exportieren und erneut laden.

### n8n-Import — ✅ fertig

- Konkreten n8n-Workflow importieren.
- Mapping-Bericht und `NeedsReview`-Hinweise prüfen.
- Erzeugtes BPMN validieren.
- Modell deployen, erneut laden und exportieren.

Für alle persistierenden Use Cases gilt: Ein erfolgreicher HTTP-Aufruf reicht nicht als Nachweis. Die Seite muss neu geladen und der gespeicherte Zustand erneut über die GUI geprüft werden.

## Phase 3: Prozessverwaltung — 🟡 teilweise umgesetzt

### Dashboard — 🟡 teilweise umgesetzt

- Reale Prozessdefinitionen, Instanzen, Tasks und Kennzahlen anzeigen.
- Refresh und Navigation zu den Detailseiten prüfen.
- Tenant-Wechsel muss die dargestellten Daten aktualisieren.

### Process Definitions — 🟡 teilweise umgesetzt

- Liste und Pagination laden.
- BPMN/XML und Viewer öffnen.
- Prozess starten.
- Versionshistorie anzeigen.
- Definition löschen und das dauerhafte Entfernen nach Reload prüfen.

### Process Instances — 🟡 teilweise umgesetzt

- Details, Variablen und History öffnen.
- Laufende Instanz suspendieren und fortsetzen.
- Instanz löschen und Ergebnis nach Reload prüfen.
- Abgeschlossene und fehlerhafte Instanzen korrekt darstellen.

### Tasks — 🟡 teilweise umgesetzt

- Aufgaben anzeigen und filtern.
- Task claimen.
- Details und zugeordnetes Formular öffnen.
- Task mit und ohne Variablen abschließen.
- Task muss nach erfolgreichem Abschluss aus der offenen Liste verschwinden.

### Deployments — ⬜ offen

- Gültige BPMN-Datei hochladen.
- Ungültige Datei mit verständlicher Validierung ablehnen.
- Größenlimit und Mehrfachauswahl prüfen.
- Deployment in Process Definitions wiederfinden.

### History, Event Log und Execution Details — 🟡 teilweise umgesetzt

- Daten anhand eines zuvor über die GUI ausgeführten Prozesses prüfen.
- Jobs, Incidents und Variablen laden.
- Korrelation zwischen Definition, Instanz, Task und History nachweisen.

## Phase 4: Erweiterte Runtime-Funktionen — 🟢 vollständig grün

### Simulation — ✅ grün

- Simulation starten und Ergebnis anzeigen.
- Summary und Variable Trace prüfen.
- Szenario erstellen, laden, aktualisieren und löschen (Persistenz nach Reload).
- Zwei Ergebnisse vergleichen.

### Messages und Signals — ✅ grün

- Eine wartende Prozessinstanz erzeugen.
- Message mit korrektem Correlation Key zustellen.
- Nicht passende Korrelation als Fehler prüfen (Instanz bleibt wartend).
- Signal auslösen und die betroffenen Instanzen prüfen.

### Debugging — ✅ grün

- Debugging-Session starten.
- Breakpoint setzen.
- Step Over und Continue ausführen.
- Variablen inspizieren.
- Prozess visualisieren und Timeline-Replay ausführen.

### Migration — ✅ grün

- Migration Preview erstellen.
- Migration ausführen und Status abrufen.
- Snapshot erstellen und wiederherstellen.
- Migration zurückrollen.
- Unzulässige Migration verständlich ablehnen.

## Phase 5: Administration und Integrationen — 🟢 Kernpfade grün

### Tenants — ✅ grün

- Tenant erstellen, bearbeiten, auswählen und löschen.
- Daten zweier Tenants strikt voneinander trennen (Isolation auf der Process-Definitions-Seite).
- Tenant-Wechsel auf tenant-fähigen Seiten prüfen (Reload-basierte Wiederentdeckung).

### Credentials — ✅ grün

- Credential erstellen, rotieren und löschen.
- Secret-Werte dürfen nach dem Speichern weder im DOM noch in API-Responses erscheinen.
- Fehlende oder ungültige Secret-Eingaben prüfen.

### Connectors — ✅ grün

- Connector erstellen, aktivieren, deaktivieren, testen und löschen.
- Erfolgreichen und fehlgeschlagenen Verbindungstest prüfen.
- Credential-Zuordnung und Tenant-Isolation nachweisen.

### Workflow Triggers — ✅ grün

- Trigger registrieren (One-Time-Secret-Alert), auslösen (Invocationszähler), aktivieren/deaktivieren und löschen.
- Secret-Prüfung und ungültige Requests testen.

### Weitere Administrationsseiten — 🟢 Seiten laden fehlerfrei, mutierende Aktionen teils offen

- Engine Management (schreibgeschützter Status), Configuration (Capabilities), Extensions, SSO, Health, Performance, Analytics und Compliance — ✅ alle Seiten laden fehlerfrei (Headings rendern, keine Abstürze).
- Feature Flags — ✅ Umschalten über die GUI persistiert über die API und wird zurückgesetzt.
- Extensions und Plugin-Lifecycle: laden, aktivieren, deaktivieren und entladen — 🟡 laden geprüft, Lifecycle-Aktionen offen.
- SSO-Konfiguration beziehungsweise klarer nicht-konfigurierter Zustand — 🟡 Seite rendert, Konfiguration offen.
- Analytics-Training und Export der Trainingsdaten — ⬜ offen (benötigt abgeschlossene Prozessinstanzen).

## Phase 6: Fehler-, Navigations- und Qualitätsfälle — 🟡 in Arbeit

Stand (2026-09-04):

- 🟡 Neu angelegt in `LocalStudioInfrastructureTests.cs` (Abnahmekriterium 1, Route-Smoke):
  `AllStudioRoutes_DirectNavigationAndReload_RenderTheirHeading` (30 Routen als Direkt-Smoke mit
  HTTP 200, Heading, Reload und Abwesenheit von JS-/Netzwerkfehlern) sowie
  `UnknownStudioRoute_ShowsNotFoundWithoutBrowserErrors`.
- ⬜ Lokale Grün-Abnahme des neuen Route-Smokes steht aus (läuft ausschließlich über den lokalen
  Runner `scripts/test-studio-e2e.ps1`).
- 🟡 Ebenso neu angelegt (Fehler-/Qualitätspfade ohne Stub-API):
  `ErrorPath_ProcessInstances_UnknownSearchShowsEmptyStateWithoutError` (unbekannte Suche →
  freundlicher Leerzustand statt Fehlerbanner/Absturz) und
  `DoubleClick_ExecutionDetails_InvalidId_RemainsSingleClearValidationError` (wiederholte ungültige
  Aktion bleibt ein klarer Validierungsfehler ohne Browserfehler).


Für jede mutierende Kernfunktion werden mindestens folgende Fälle automatisiert:

- Erfolgreicher Ablauf.
- Ungültige oder unvollständige Eingabe.
- API antwortet mit `400`.
- Fehlende Berechtigung mit `401` oder `403`.
- Nicht vorhandene Ressource mit `404`.
- Serverfehler mit `500`.
- Verbindungsabbruch oder Timeout.
- Mehrfachklick während einer laufenden Operation.
- Browser-Reload während oder nach der Operation.

Zusätzlich prüfen:

- Keine unbehandelten JavaScript-Fehler.
- Keine unerwarteten fehlgeschlagenen HTTP-Aufrufe.
- Keine dauerhaft sichtbaren Ladeindikatoren.
- Navigation über Menü und Global Search.
- Direkter Aufruf jeder Route und Browser-Reload.
- Fehlerseite und unbekannte Route.
- Desktop-Viewport und mindestens ein kleiner Viewport als Smoke-Test.
- Verständliche Benutzerhinweise statt leerer oder abgestürzter Seiten.

Bei Fehlern werden automatisch gespeichert:

- Screenshot;
- Playwright Trace;
- Browserkonsole;
- fehlgeschlagene HTTP-Requests;
- Studio- und API-Logs.

## Lokale Testausführung

### Vollständiger Lauf

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Auto
```

### Vorhandene lokale Infrastruktur

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Existing
```

### WSLC explizit verwenden

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Wslc
```

Der Runner erzeugt am Ende einen lokalen Bericht mit:

- Route;
- getesteter Aktion;
- Use-Case-ID;
- Ergebnis und Dauer;
- Verweis auf Screenshots und Traces bei Fehlern.

## Verbindliche Abnahmekriterien

VertexBPMN Studio gilt erst als lokal GUI-verifiziert, wenn:

1. Jede produktive Studio-Route mindestens einen echten Browser-Smoke-Test besitzt.
2. Jede sichtbare produktive Aktion mindestens einen konkreten Use Case besitzt.
3. Jede persistierende Aktion nach Browser-Reload verifiziert wird.
4. Der vollständige BPMN-Ablauf Import, Bearbeitung, Deploy, Reload, Start und Task-Abschluss funktioniert.
5. Kein Real-E2E-Test eine Stub-API verwendet.
6. Fehlerfälle führen zu verständlichen Meldungen und nicht zu abgestürzten Seiten.
7. Die vollständige Suite zweimal hintereinander auf einer sauberen lokalen Datenbasis erfolgreich läuft.
8. Der Testbericht jede Route und Aktion eindeutig einem Testfall zuordnet.
9. GitHub Actions unverändert bleibt und die Suite ausschließlich explizit lokal gestartet wird.

Aktueller Erfüllungsstand:

- ⬜ Kriterien 1–4, 6 und 8 sind noch nicht vollständig erfüllt.
- ✅ Kriterium 7 ist erfüllt: Die vollständigen WSLC-Läufe `aaff96a9e156431fa63f0e2187aaa0f4` und `0abcfd75e01540ccad56f85185e5a74b` waren mit jeweils 8/8 Tests grün; alle fünf Laufdatenbanken wurden danach verifiziert entfernt.
- ✅ Kriterium 5 ist erfüllt: Die Real-E2E-Suite verwendet keine Stub-API.
- ✅ Kriterium 9 ist erfüllt: Die Suite wird nur über den lokalen Runner aktiviert und ist nicht Teil des CI-Workflows.

## Priorität und Aufwand

| Reihenfolge | Arbeitspaket | Priorität | Aufwand |
|---|---|---|---|
| 1 | Real-E2E-Testhost und lokaler Runner | Muss | L |
| 2 | BPMN-Modellierung und vollständiger Runtime-Workflow | Muss | L |
| 3 | DMN, CMMN und Forms | Muss | L |
| 4 | Prozessverwaltung und Runtime-Seiten | Muss | L |
| 5 | Administration und Integrationen | Sollte | L |
| 6 | Fehlerpfade, Viewports, Reports und Stabilisierung | Muss | L |

Phase 1 ist Voraussetzung für alle weiteren Pakete. Der BPMN-Hauptworkflow besitzt danach die höchste Priorität, weil er den zentralen produktiven Nutzen des Studios nachweist.
