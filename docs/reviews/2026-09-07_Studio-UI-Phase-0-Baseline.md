# Studio UI: Phase-0-Baseline

Stand: 2026-09-07. Status: abgeschlossen.

## 1. Umfang und Methode

Diese Baseline schützt den Funktionsumfang vor dem Redesign und dokumentiert den sichtbaren Ausgangszustand. Sie umfasst:

- statische Inventur aller Razor-Seiten, Shell-, Navigations-, Modellierer- und Dialogkomponenten;
- Zuordnung der vorhandenen Routen und primären Aktionen;
- Prüfung der vorhandenen lokalen UI-Testabdeckung;
- reproduzierbare Screenshots des echten Blazor-Studios mit dem vorhandenen isolierten UI-Testhost und dessen definierten Testdaten;
- Messung von Dokumentbreite/-höhe bei 1440×900 und 390×844;
- Ausführung der vorhandenen Studio-Contract-Suite.

Die Screenshot-Baseline verwendet die echte Studio-Anwendung, aber eine Stub-API mit deterministischen Daten. Sie ist deshalb eine Darstellungs- und UI-Vertragsbaseline, kein Beleg für echte Backendintegration. Der reale AppHost wurde zusätzlich über `scripts/wslc-apphost.ps1` gestartet: Projekte bauten ohne Fehler und WSLC PostgreSQL/RabbitMQ waren bereit, Aspire erreichte aber innerhalb von 120 Sekunden keinen gestarteten AppHost (letzte Ausgabe vor dem Timeout: Zertifikatsvertrauen). Dieser Startfehler ist getrennt vom UI-Redesign zu behandeln.

## 2. Seiten- und Funktionsinventur

Es existieren 31 Razor-Seitenrouten: 30 fachliche/direkt getestete Ziele plus `/Error`. 29 Ziele erscheinen bei aktivierter CMMN-Capability gleichzeitig als flache Navigationslinks. `/counter` und `/Error` stehen nicht in der Navigation.

| Arbeitsbereich | Route | Vorhandene Kernaktionen, die erhalten bleiben müssen |
|---|---|---|
| Übersicht | `/` | Laufzeitkennzahlen laden, aktualisieren, Tasks/Definitionen öffnen |
| Modellieren | `/bpmn-modeler` | BPMN deployen, importieren/exportieren, validieren, Katalog/Patterns, Simulation, Engine-Test, XML, Versionen, Viewer |
| Modellieren | `/dmn-modeler` | DMN deployen/exportieren/laden, Regel ergänzen, evaluieren |
| Modellieren | `/cmmn-modeler` | Case-Modell registrieren/exportieren, Case ausführen, History, User Event, Case File, Ad-hoc-Subprozess |
| Modellieren | `/form-builder` | Formular speichern/exportieren/laden, Felder ergänzen, Runtime Viewer |
| Arbeiten | `/tasks` | suchen/sortieren, Task übernehmen, abschließen, Details öffnen |
| Arbeiten | `/process-definitions` | suchen/filtern/gruppieren, aktualisieren, BPMN ansehen, Prozess starten, Versionen, löschen |
| Arbeiten | `/process-instances` | suchen/sortieren, aktualisieren, History, suspendieren/fortsetzen, Vorfallzustände, löschen |
| Arbeiten | `/deployments` | BPMN-Dateien auswählen/prüfen/deployen |
| Arbeiten | `/triggers` | Trigger registrieren, testen, aktivieren/deaktivieren, löschen |
| Arbeiten | `/messages-signals` | Nachricht korrelieren, Signal senden |
| Analysieren | `/history` | History suchen/aktualisieren |
| Analysieren | `/execution-details` | Jobs, Incidents und Variablen laden |
| Analysieren | `/event-log` | Eventlog aktualisieren |
| Analysieren | `/analytics` | aktualisieren, Modell trainieren, Trainingsdaten exportieren |
| Analysieren | `/performance` | Betriebs-/Performancedaten aktualisieren |
| Analysieren | `/health` | Health-/Operationsdaten aktualisieren |
| Analysieren | `/simulation` | Simulation ausführen, Szenarien CRUD, analysieren und vergleichen |
| Analysieren | `/debugging` | Trace/Session, Breakpoint, Step-over, Continue, Visualisierung, Variablen, Replay |
| Analysieren | `/compliance` | Compliance-Evidence anzeigen |
| Verwalten | `/engine-management` | Engine-Verbindungen verwalten/anzeigen |
| Verwalten | `/tenants` | Tenants aktualisieren, erstellen, bearbeiten, löschen |
| Verwalten | `/credentials` | Credentials erstellen, OAuth verbinden, rotieren, löschen |
| Verwalten | `/configuration` | Konfiguration anzeigen |
| Verwalten | `/feature-flags` | Flags laden und ändern |
| Verwalten | `/migration` | Preview, Migration, Status, Snapshot, Restore, Rollback |
| Verwalten | `/connectors` | Connectoren erstellen, testen, aktivieren/deaktivieren, löschen |
| Verwalten | `/extensions` | Extensions laden, aktivieren/deaktivieren und entladen |
| Verwalten | `/sso` | SSO-Status/-Konfiguration anzeigen |
| Sonstige | `/counter` | Template-Counter; produktiven Zweck vor späterer Entfernung klären |
| Fehler | `/Error` | Fehlerzustand darstellen |

Zusätzliche funktionsrelevante Komponenten: `StartProcessDialog`, `TaskDetailsDialog`, `ProcessInstanceDetailsDialog`, `ProcessInstanceHistoryDialog`, `ProcessVersionsDialog`, `BpmnViewerDialog`, `GlobalSearch`, vier Modeler-Surfaces und zugehörige Viewer-Surfaces. Diese müssen in die Paritätsmatrix jeder späteren Phase einbezogen werden.

## 3. Reproduzierbare visuelle Baseline

Der neue Test `StudioVisualBaselineTests` ist bewusst opt-in (`VERTEXBPMN_UI_BASELINE_TESTS=true`) und läuft nicht automatisch. Artefakte werden lokal nach `tests/VertexBPMN.Studio.UiTests/TestResults/ui-modernization-baseline/` geschrieben.

| Ansicht | Viewport | Dokument | Sichtbare Navigationslinks | Beobachtung |
|---|---:|---:|---:|---|
| Dashboard | 1440×900 | 1440×900 | 29 | Kein Overflow; 29 ungruppierte Links und große weitgehend leere Content-Flächen. |
| BPMN | 1440×900 | 1440×5071 | 29 | Kernworkflow verteilt sich vertikal über mehr als fünf Viewport-Höhen; Canvas, XML, Versionen und Viewer konkurrieren. |
| Tasks | 1440×900 | 1440×900 | 29 | Funktional, aber technische GUIDs dominieren die visuelle Hierarchie. |
| Dashboard | 390×844 | 390×1784 | 0 | Inhalt reflowt; Kopfzeile ist horizontal abgeschnitten, Engine-Kontext nicht vollständig sichtbar. |
| BPMN | 390×844 | **660×3520** | 0 | Kritischer horizontaler Overflow; Toolbar und Properties Panel sind nicht für 390 px ausgelegt, Arbeitscanvas wird praktisch unbenutzbar. |
| Tasks | 390×844 | 390×844 | 0 | Tabelle wechselt in Kartenform; Kopfzeile bleibt abgeschnitten. |

Screenshots:

- `dashboard-desktop.png`, `dashboard-mobile.png`
- `bpmn-modeler-desktop.png`, `bpmn-modeler-mobile.png`
- `tasks-desktop.png`, `tasks-mobile.png`
- maschinenlesbare Werte in `layout-metrics.json`

## 4. Priorisierte UI-Befunde

| Priorität | Befund | Evidenz | Ziel für das Redesign |
|---|---|---|---|
| P0 | BPMN ist mobil breiter als der Viewport und die nutzbare Modellierfläche kollabiert. | 660 px Dokument bei 390 px Viewport; Screenshot `bpmn-modeler-mobile.png` | Kein unkontrollierter Seitenoverflow; Modellierer erhält eigenen adaptiven Workspace. |
| P0 | Tenant-/Engine-Kontext wird in kleinen Viewports abgeschnitten. | alle Mobile-Screenshots | Kompakte, zugängliche Kontextauswahl in responsiver AppBar/Context-Surface. |
| P0 | BPMN-Primärworkflow ist auf Desktop 5071 px hoch. | `bpmn-modeler-desktop.png` und Metrik | Canvas bleibt primär; Sekundärwerkzeuge in zustandserhaltenden Panels/Tabs. |
| P1 | 29 gleichrangige Navigationsziele erzeugen hohe Scanlast. | Desktop-Screenshots und `NavMenu.razor` | Gruppierung nach Modellieren, Arbeiten, Analysieren und Verwalten; URLs bleiben gleich. |
| P1 | Einheitlicher `MaxWidth.Large`-Container behandelt Editor, Tabelle und Formular gleich. | `MainLayout.razor` | Drei explizite Layouttypen mit identischer Shell. |
| P1 | Technische IDs besitzen in Listen dieselbe oder größere visuelle Bedeutung als Aufgabenname/Status. | Tasks-Screenshots | Nutzerdaten priorisieren; IDs vollständig zugänglich, aber sekundär. |
| P1 | Dashboard nutzt große Kartenflächen mit geringer Informationsdichte. | Dashboard-Screenshots | Kompaktere Übersicht ohne neue oder erfundene KPIs. |
| P2 | Default-MudBlazor-Look, viele gleich starke Akzentfarben und uneinheitliche Seitentitel. | Screenshots, `new MudTheme()`, Seitenquellen | Eigenes Token-/Theme-System und gemeinsame PageHeader-/Toolbar-Komponenten. |
| P2 | Roboto wird geladen, global setzt `app.css` Helvetica; Bootstrap und MudBlazor sind parallel aktiv. | `App.razor`, `app.css` | Eine definierte Typografiekette; Bootstrap erst nach Nutzungsinventur selektiv entfernen. |
| P2 | Gemischte deutsch-/englischsprachige Aktionen und Zustände. | z. B. Analytics gegenüber übriger Shell | Sprachstrategie separat entscheiden; keine Übersetzung als stiller Teil des visuellen Refactorings. |

## 5. Funktionsschutz und Testergebnis

Ausgeführt:

- `StudioVisualBaselineTests`: **1/1 erfolgreich**, sechs Screenshots und Messdatei erzeugt.
- `StudioUiContractTests`: **21/21 erfolgreich** nach Aktualisierung veralteter Tests auf das bereits gültige Editorverhalten.
- Build `VertexBPMN.Studio.UiTests` Release mit `SkipBpmnIoAssetBuild=true`: **0 Fehler**. Vorhanden bleiben zwei ältere xUnit-Analyzerwarnungen in MultiInstance/SubProcesses sowie NU1900, weil NuGet-Sicherheitsdaten nicht erreichbar waren.

Die Contract-Tests wurden nicht gelockert:

- Katalogelemente wählen jetzt wie das Produkt einen echten Sequenzfluss vor der Einfügung.
- Quick-Insert belegt die vollständige IF-Struktur: fünf Flows, Split und Merge, Then-Task, Default-Flow und Condition-Flow.
- Der Error-Handler-Test beweist die gewollte Ablehnung einer isolierten Event-Subprozess-Einfügung und die unveränderte XML-Struktur.
- Process Definitions prüft die tatsächlich dargestellten Grid-Daten statt das entfernte Kartenformat.

Die umfangreiche WSLC-Real-E2E-Suite ist vorhanden, wurde in dieser Phase aber nicht vollständig erneut ausgeführt. Vor einer produktiven UI-Änderung an Phase 2/3 wird ein passender Real-E2E-Satz erneut ausgeführt.

## 6. Abnahme Phase 0

- [x] Alle Seitenrouten und zusätzlichen Dialog-/Surface-Komponenten inventarisiert.
- [x] Bestehende Aktionen und fachliche Invarianten als Redesign-Grenze erfasst.
- [x] Desktop-/Mobile-Baseline für Dashboard, BPMN und Tasks erzeugt.
- [x] Overflow und Dokumentgeometrie reproduzierbar gemessen.
- [x] Bestehende Contract-Tests grün; veraltete Erwartungen korrigiert, ohne Assertions zu entfernen.
- [x] Bekannte Infrastrukturabweichung (AppHost-Timeout) getrennt dokumentiert.

Nächster Schritt: Phase 1 erstellt drei konkrete, zusammengehörige Entwürfe für Shell/Dashboard, BPMN-Workspace und Tasks – jeweils Desktop und Mobile – auf Grundlage dieser Baseline.
