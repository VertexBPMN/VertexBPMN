# Plan: Vollständige lokale GUI-End-to-End-Tests

## Ziel

Jede produktive Funktion von VertexBPMN Studio wird im echten Browser gegen die echte API, Engine und Persistenz geprüft. Die vorhandenen Browser-Contract-Tests mit Stub-API bleiben als schnelle Tests erhalten, reichen als Nachweis der Gebrauchstauglichkeit aber nicht aus.

Die neue Real-E2E-Suite läuft ausschließlich lokal. Sie wird nicht in GitHub Actions oder andere CI-Workflows aufgenommen.

## Ausgangslage

- Das bestehende Projekt `tests/VertexBPMN.Studio.UiTests` verwendet echtes Chromium und startet den echten Studio-Prozess.
- Import, grundlegende Bearbeitung und Export von BPMN sind im Browser abgedeckt.
- Repository-, Runtime- und Task-Endpunkte werden im aktuellen UI-Testhost jedoch durch eine Stub-API ersetzt.
- Reale Persistenz, erneutes Laden und vollständige Workflows über Browser, API und Engine sind daher noch nicht nachgewiesen.
- Die bestehende Browser-Suite ist nicht Bestandteil des schnellen CI-Workflows. Das soll auch für die Real-E2E-Suite gelten.

## Phase 1: Lokale Real-E2E-Infrastruktur

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
- Einen lokalen Einstiegspunkt bereitstellen:

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Auto
```

### Anforderungen

- Keine Stub-API innerhalb der Real-E2E-Suite.
- Fehlende PostgreSQL-/RabbitMQ-Voraussetzungen führen zu einem verständlichen Preflight-Fehler und nicht zu übersprungenen Tests.
- Keine Änderung an `.github/workflows/ci.yml`.
- Die Tests dürfen nicht durch den normalen schnellen `dotnet test tests/VertexBPMN.Tests/...`-Aufruf gestartet werden.

## Phase 2: Kritische Modellierungs-Use-Cases

### BPMN Modeler

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

### BPMN Runtime

1. Ein ausführbares BPMN-Modell über die GUI deployen.
2. Den Prozess über die GUI starten.
3. Die erzeugte Prozessinstanz in der Instanzseite finden.
4. Einen User Task auf der Task-Seite finden und claimen.
5. Formulardaten eingeben und den Task abschließen.
6. Den abgeschlossenen Prozessstatus, Variablen, History und Event Log prüfen.

### DMN Modeler

- DMN importieren oder neu erstellen.
- Decision Table über den Modeler verändern.
- DMN deployen und nach einem Browser-Reload aus der API laden.
- Entscheidungen mit konkreten Eingabedaten auswerten.
- Positive, negative und No-Match-Ergebnisse prüfen.
- DMN exportieren und erneut importieren.

### CMMN Modeler

- CMMN-Modell laden, bearbeiten und registrieren.
- Case starten.
- User Event auslösen.
- Case File Item aktualisieren.
- Ad-hoc-Subprozess erzeugen.
- Historie laden und Zustandsänderungen prüfen.
- CMMN exportieren und erneut importieren.

### Form Builder

- Formular erstellen und Felder konfigurieren.
- Formular speichern und nach Browser-Reload erneut laden.
- Gespeichertes Formular verändern und aktualisieren.
- Runtime Viewer prüfen.
- JSON exportieren und erneut laden.

### n8n-Import

- Konkreten n8n-Workflow importieren.
- Mapping-Bericht und `NeedsReview`-Hinweise prüfen.
- Erzeugtes BPMN validieren.
- Modell deployen, erneut laden und exportieren.

Für alle persistierenden Use Cases gilt: Ein erfolgreicher HTTP-Aufruf reicht nicht als Nachweis. Die Seite muss neu geladen und der gespeicherte Zustand erneut über die GUI geprüft werden.

## Phase 3: Prozessverwaltung

### Dashboard

- Reale Prozessdefinitionen, Instanzen, Tasks und Kennzahlen anzeigen.
- Refresh und Navigation zu den Detailseiten prüfen.
- Tenant-Wechsel muss die dargestellten Daten aktualisieren.

### Process Definitions

- Liste und Pagination laden.
- BPMN/XML und Viewer öffnen.
- Prozess starten.
- Versionshistorie anzeigen.
- Definition löschen und das dauerhafte Entfernen nach Reload prüfen.

### Process Instances

- Details, Variablen und History öffnen.
- Laufende Instanz suspendieren und fortsetzen.
- Instanz löschen und Ergebnis nach Reload prüfen.
- Abgeschlossene und fehlerhafte Instanzen korrekt darstellen.

### Tasks

- Aufgaben anzeigen und filtern.
- Task claimen.
- Details und zugeordnetes Formular öffnen.
- Task mit und ohne Variablen abschließen.
- Task muss nach erfolgreichem Abschluss aus der offenen Liste verschwinden.

### Deployments

- Gültige BPMN-Datei hochladen.
- Ungültige Datei mit verständlicher Validierung ablehnen.
- Größenlimit und Mehrfachauswahl prüfen.
- Deployment in Process Definitions wiederfinden.

### History, Event Log und Execution Details

- Daten anhand eines zuvor über die GUI ausgeführten Prozesses prüfen.
- Jobs, Incidents und Variablen laden.
- Korrelation zwischen Definition, Instanz, Task und History nachweisen.

## Phase 4: Erweiterte Runtime-Funktionen

### Simulation

- Simulation starten und Ergebnis anzeigen.
- Summary und Variable Trace prüfen.
- Szenario erstellen, laden, aktualisieren und löschen.
- Zwei Ergebnisse vergleichen.

### Messages und Signals

- Eine wartende Prozessinstanz erzeugen.
- Message mit korrektem Correlation Key zustellen.
- Unbekannte und mehrdeutige Korrelation als Fehler prüfen.
- Signal auslösen und die betroffenen Instanzen prüfen.

### Debugging

- Debugging-Session starten.
- Breakpoint setzen.
- Step Over und Continue ausführen.
- Variablen inspizieren.
- Prozess visualisieren und Timeline-Replay ausführen.

### Migration

- Migration Preview erstellen.
- Migration ausführen und Status abrufen.
- Snapshot erstellen und wiederherstellen.
- Migration zurückrollen.
- Unzulässige Migration verständlich ablehnen.

## Phase 5: Administration und Integrationen

### Tenants

- Tenant erstellen, bearbeiten, auswählen und löschen.
- Daten zweier Tenants strikt voneinander trennen.
- Tenant-Wechsel auf allen tenant-fähigen Seiten prüfen.

### Credentials

- Credential erstellen, rotieren und löschen.
- Secret-Werte dürfen nach dem Speichern weder im DOM noch in API-Responses erscheinen.
- Fehlende oder ungültige Secret-Eingaben prüfen.

### Connectors

- Connector erstellen, aktivieren, deaktivieren, testen und löschen.
- Erfolgreichen und fehlgeschlagenen Verbindungstest prüfen.
- Credential-Zuordnung und Tenant-Isolation nachweisen.

### Workflow Triggers

- Trigger registrieren, aktivieren, deaktivieren, auslösen und löschen.
- Secret-Prüfung und ungültige Requests testen.

### Weitere Administrationsseiten

- Engine Management und Configuration.
- Feature Flags.
- Extensions und Plugin-Lifecycle: laden, aktivieren, deaktivieren und entladen.
- SSO-Konfiguration beziehungsweise klarer nicht-konfigurierter Zustand.
- Health, Performance, Analytics und Compliance.
- Analytics-Training und Export der Trainingsdaten.

## Phase 6: Fehler-, Navigations- und Qualitätsfälle

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
