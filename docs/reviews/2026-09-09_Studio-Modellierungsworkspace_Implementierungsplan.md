# Studio zum professionellen Modellierungsworkspace weiterentwickeln

Stand: 2026-09-09. Planungsbasis: `71d9889e07f72f5ed7bc0919eb699749480fd7a4`.
Status: **Plan erstellt; keine Umsetzung dieses Plans begonnen.**

Dieser Plan ist ein eigenständiger, modellunabhängiger Arbeitsauftrag. Er kann von einem Menschen oder einem Coding-Agent mit Repository-, Terminal- und lokalem Browserzugang bearbeitet werden. Vorheriger Chatverlauf ist nicht erforderlich. Alle Pfade sind repository-relativ; neue Dateien und Verträge sind ausdrücklich als Vorschlag gekennzeichnet.

## 1. Ziel und Abgrenzung

VertexBPMN Studio soll eine verlässliche, verständliche Modellierungsumgebung für die VertexBPMN-Engine werden. Der vorhandene Blazor-/MudBlazor-Rahmen und bpmn-js bleiben bestehen. Kein Fork des Camunda Desktop Modelers, kein Electron-Umbau, kein Austausch der ProcessEngine.

Das erste Lieferziel ist der **BPMN-Workspace**: Modelle finden, als Entwurf bearbeiten und speichern, nach Unterbrechungen wiederherstellen, fachlich konfigurieren, prüfen und gezielt deployen. DMN, CMMN und Formulare bleiben funktionsfähig, werden aber nicht gleichzeitig neu entwickelt. Wiederverwendbare Bausteine sollen deren späteren Ausbau ermöglichen.

Dieser Plan ergänzt, ersetzt aber nicht:

- [Bisherigen UI-Modernisierungsplan](2026-09-07_Studio-UI-Modernisierungsplan.md): dort abgeschlossene Arbeiten nicht pauschal erneut implementieren.
- [BPMN-Editor-Korrekturstatus](2026-09-07_BPMN-Editor-Korrekturstatus.md): korrigierte Einfüge-, Validierungs- und Undo-Pfade erhalten.
- [Lokalen GUI-E2E-Testplan](2026-09-01_Lokaler_GUI_E2E_Testplan.md).
- [Produktionsqualitätsplan](2026-09-08_Produktionsqualitaet_Release-Abnahmeplan.md): dessen Sicherheits-, Ausfall- und Lastabnahmen bleiben eigenständig.
- [Aktuelle Sicherheitsgrenzen](2026-09-09_Phase3_Restpunkte_Abschluss.md): Export-Redaktion, Tenant-/Rollenschutz und Credential-Behandlung nicht umgehen.

**Neue Funktionalität:** Servergespeicherte Entwürfe und Konflikterkennung sind mehr als ein kosmetisches Redesign. Die dafür notwendigen API-/Persistenzänderungen sind nur in W02/W04 vorgesehen und vor Implementierung vertraglich festzuhalten. Dieser Plan selbst ist keine Aufforderung, jetzt Code zu ändern, Services zu starten oder Daten zu migrieren.

## 2. Verifizierter Einstiegspunkt und verbleibende Ungewissheit

Für diese Planung wurden Dateien gelesen, aber keine neue Live-UI-Abnahme und kein Build ausgeführt. Frühere grüne Tests sind historische Nachweise, keine Abnahme künftiger Änderungen.

| Vorhanden / beobachtet | Einstiegspunkt | Konsequenz |
|---|---|---|
| Gepinnte bpmn-js-, Properties-, DMN-, CMMN- und Form-Bibliotheken | `src/VertexBPMN.Studio/package.json` | Keine Bibliotheksmigration als Nebenarbeit. |
| BPMN-Seite mit Import, Export, Deployment, Validierung, Katalog, Simulation und Versionsvergleich | `src/VertexBPMN.Studio/Components/Pages/BpmnModelerPage.razor` | Bestehende Aktionen inventarisieren und erhalten. |
| Blazor-Wrapper mit Import/GetXml, Resize und Dispose | `src/VertexBPMN.Studio/Components/Modeling/BpmnModelerSurface.razor` | Hier die Dokument-/Interop-Grenze klären, nicht einen zweiten Editor einführen. |
| JavaScript-Editoranbindung | `src/VertexBPMN.Studio/wwwroot/js/bpmn-modeler.js` | Handgeschriebene Interop ist Quellcode; nicht mit generierten Bibliotheksbundles verwechseln. |
| Vertex-XML-Schema, Properties und Clientvalidierung | `src/VertexBPMN.Studio/tools/bpmn-io/src/{vertex.json,vertex-properties-provider.js,vertex-validation.js,flow-validation.js}` | Gemeinsame Grundlage weiterverwenden. |
| Serverseitige Deploymentvalidierung | `src/VertexBPMN.Domain/Model/Bpmn/BpmnDeploymentValidator.cs`, `src/VertexBPMN.Api/Controllers/RepositoryController.cs` | Server bleibt entscheidend für Deployment, Clientprüfung allein genügt nicht. |
| Expliziter Export/Deploy und Laden deployter Versionen | `BpmnModelerPage.razor`: `ExportBpmnXml`, `DeployBpmnXml`, `LoadSelectedVersionAsync` | Download ist kein dauerhaft gespeicherter Entwurf. Eine vollständige Entwurfs-/Konfliktstrecke wurde in den geprüften Dateien nicht identifiziert; W01 muss repo-weit verifizieren. |
| Theme und Tokens bereits vorhanden | `src/VertexBPMN.Studio/Styling/StudioTheme.cs`, `wwwroot/css/studio-tokens.css` | Bestehendes Designsystem erweitern, keine zweite CSS-/Komponentenwelt. |
| Modeler bleibt beim Werkzeugwechsel gemountet | `docs/reviews/2026-09-07_Studio-UI-Phase-3-BPMN-Pilot.md` | Auswahl und Undo/Redo dürfen durch Layoutarbeit nicht verloren gehen. |
| Lokale Tests und WSLC-/Existing-Einstieg vorhanden | `tests/VertexBPMN.Studio.UiTests/`, `scripts/test-studio-e2e.ps1` | Echte GUI-Tests lokal, keine neuen CI-Gates. |

## 3. Verbindliche Arbeitsregeln für jeden Ausführenden

1. Aktuellen Branch, Commit, `git status`, Repository-Anweisungen und dieses Dokument lesen. Abweichungen von der Planungsbasis erfassen. Fremde Änderungen nicht überschreiben.
2. Genau ein W-Arbeitspaket aktiv bearbeiten. Bestehende Implementierung zuerst suchen; bereits erfüllte Kriterien durch Belege schließen, nicht neu bauen.
3. Fehler zuerst reproduzieren. Keine Tests löschen, skippen oder abschwächen, um grün zu werden; keine festen Sleeps als Lösung für Zustands-/Konkurrenzfehler.
4. Paket-Upgrades, Auth-Umbauten, Engine-Semantik, globale Styles und API-Vertragsänderungen außerhalb des jeweiligen Pakets sind nicht implizit erlaubt. Neue notwendige Entscheidungen im Übergabebericht nennen und bei wesentlicher Scope-Erweiterung Rückfrage halten.
5. Produktionslogik und Persistenz nicht mocken, wenn die Abnahme ihre tatsächliche Wirkung behauptet. Isolierte UI-Verträge dürfen einen Testserver verwenden, müssen aber so beschriftet sein.
6. Generierte Assets unter `wwwroot/lib/` nicht manuell editieren. Quellen unter `tools/bpmn-io/src/` ändern und die bestehende Buildpipeline ausführen. Lockfile und Bundles nur bei tatsächlichen Änderungen aufnehmen.
7. Keine Secrets in Modelle, Fixtures, Logs, Screenshots oder Übergaben aufnehmen. Nur isolierte Testkonten und Testdaten verwenden. Benutzer-Dienste und Datenbanken nicht stoppen oder entfernen.
8. Kein automatischer Push/PR und keine Branch-Löschung ohne gesonderten Nutzerauftrag. Vor Commit alle betroffenen Tests prüfen und nur zugehörige Dateien stagen.
9. Ein anderes Modell ist kein unabhängiger menschlicher Security-Reviewer. Grüne GUI-Tests sind weder vollständige Standardkonformität noch Produktionsfreigabe.

## 4. Zielarchitektur und Zustandsvertrag

### Zuständigkeiten

- **bpmn-js:** einziges veränderbares grafisches Dokumentmodell einschließlich Command-Stack, Auswahl und Undo/Redo.
- **Blazor:** Workspace, Navigation, Metadaten, Dialoge, Berechtigungen und Darstellung von Status/Diagnosen; hält Snapshots, kein konkurrierendes Live-BPMN-Modell.
- **Dokumentsitzung (neu, vorgeschlagener Baustein):** Identität aus Engine-/API-Kontext, Tenant, Benutzer und Dokument; lokale Revision, zuletzt gespeicherte Revision, Server-Revision und laufende Operationen.
- **Entwurfsdienst (neu oder vorhandenen Dienst erweitern):** persistente Entwürfe mit atomarer optimistischer Konkurrenzprüfung; keine Engine-Deployment-Nebenwirkung beim Speichern.
- **Repository-/Deploymentdienst:** vorhandene Engine-Schnittstelle für geprüfte Deployments und unveränderliche deployte Versionen.

### Zustandsregeln

- Anzeigen: `Lädt`, `Unverändert`, `Ungespeichert`, `Speichert`, `Gespeichert`, `Speicherfehler`, `Konflikt`, `Verbindung unterbrochen`. Status nicht nur durch Farbe vermitteln.
- Jede fachliche Dokumentänderung erhöht eine lokale Revision. Zoom, Auswahl und Panelwechsel machen das Modell nicht fachlich dirty.
- Beim Speichern Snapshot und Revision festhalten. Eine verspätete Antwort für Revision N darf Änderungen N+1 niemals als gespeichert markieren. Mehrere Saves serialisieren; alte Antworten nach Dokument-/Tenantwechsel verwerfen.
- XML-Expertenmodus ist zunächst ein separater Textpuffer. Erst „Übernehmen“ mit erfolgreichem Import ersetzt das Diagramm. Bei Fehler bleibt das bisherige Diagramm erhalten. W02 legt das Verhalten des Undo-Stacks bei vollständigem XML-Ersatz sichtbar fest.
- Import, Versionswechsel, Navigation und Tenant-/Enginewechsel dürfen ungesicherte Arbeit nicht stillschweigend verwerfen. Abbruch eines Kontextwechsels muss möglich sein, bevor Daten des Zielkontexts geladen werden.
- Benutzer-/Tenant-/Enginewechsel dürfen weder den alten Entwurf anzeigen noch ihn im neuen Kontext speichern. Serverautorisierung ist unabhängig von der UI obligatorisch.
- Ungültige **BPMN-Ausführungslogik** darf als Entwurf gespeichert werden; XML-/Größen-/Zugriffsschutz bleibt aktiv. Unparsebarer XML-Text darf nur als ausdrücklich separater Wiederherstellungspuffer geführt werden, niemals als erfolgreich importiertes Diagramm.
- Deployment verwendet einen festgehaltenen Snapshot. Währenddessen neu editierte Änderungen gehören nicht rückwirkend zu diesem Deployment.

### Speicher- und Sicherheitsentscheidung

Ziel für den ersten Ausbau: explizites **serverseitiges Entwurfsspeichern** und serverseitige Wiederherstellung. Lokales dauerhaftes Autosave ist **nicht standardmäßig Teil des ersten Releases**, weil BPMN Inhalte und Zugangsdaten enthalten kann. W02 muss vor einer späteren Browserpersistenz Aufbewahrung, Logout/Löschung, Shared-PC-Verhalten, Größenlimits und Secret-Policy entscheiden. Bei Offline-/Circuit-Abbruch keine Speicherzusage; Zustand im noch lebenden Browser erhalten und Wiederverbindung ermöglichen. Ohne bestätigten Server-Save ist Wiederherstellung nach Tab-/Browserverlust nicht garantiert und muss so kommuniziert werden.

Die neue Entwurfsstrecke darf keine Hintertür an der Export-Redaktion vorbei schaffen: Credential-Referenzen verwenden. Bei erkannten eingebetteten Secrets sichere Bereinigung/Überführung anbieten, keine stille destruktive Maskierung mit anschließendem „Gespeichert“. Opaque Skripte sind kein zuverlässig automatisch lösbares Secret-Scanning-Problem. W02 definiert die Policy samt Zugriff und Tests, bevor Roh-XML gespeichert/ausgeliefert wird.

## 5. Arbeitspakete und Abhängigkeiten

Aufwand ist eine relative Schätzung: S = bis etwa 2, M = etwa 3–5, L = etwa 6–10 konzentrierte Entwicklertage inklusive Tests. Keine garantierte Agent-Laufzeit. W01 darf nach Befund begründet neu schätzen. Kritischer Pfad: W01 → W02 → W03 → W04 → W07 → W09. W05/W06/W08 können nach ihren Voraussetzungen unabhängig vorbereitet werden, aber nicht dieselben Dateien gleichzeitig verändern.

| ID | Paket | Priorität | Aufwand | Voraussetzung |
|---|---|---|---|---|
| W01 | Ist-Inventur und reproduzierbare Baseline | Muss | M | keine |
| W02 | Dokument-, Entwurfs- und Sicherheitsvertrag | Muss | M | W01 |
| W03 | Dokumentsitzung und stabile Interop | Muss | L | W02 |
| W04 | Entwürfe, Konflikte und Wiederherstellung | Muss | L | W03; W02-Vertrag |
| W05 | Gemeinsame Vertex-Editorfunktionen und Properties | Muss | L | W02; Integration nach W03 |
| W06 | Diagnosen und verlustkontrollierter Roundtrip | Muss | L | W03, W05 |
| W07 | Modellnavigation, Versionen und sicheres Deployment | Muss | M | W04, W06 |
| W08 | Workspace-Bedienung und visuelle Abnahme | Muss | M | W03; Endabnahme nach W07 |
| W09 | Lokale Gesamtabnahme und Übergabe | Muss | M | W04–W08 |
| W10 | Spätere Erweiterungen | Kann | separat | W09 und Nutzerentscheidung |

### W01 – Ist-Inventur und Baseline

- [ ] Für alle vorhandenen BPMN-Aktionen eine Tabelle erstellen: UI-Einstieg → Interop/Service → API/Persistenz → aktueller Test → belegter Status. Insbesondere Draft-/Autosave-/ETag-Funktionen repo-weit suchen.
- [ ] Vorhandene UI-Komponenten, Theme-Tokens, Fixtures und Testhost-Modi prüfen. Mock-UI und echte API/Engine klar trennen.
- [ ] Reale Kernabläufe lokal aufnehmen: Import, Properties-Änderung, Undo/Redo, Validierung/Fehlerfokus, Export/Reimport, Deployment/Reload, Versionsvergleich, Simulation und Engine-Test.
- [ ] Screenshots Desktop 1440×900 und schmal 390×844 aufnehmen; aktuelle Importzeiten und API-Aufrufzahlen messen. Fehler mit Reproduktion statt Vermutung erfassen.

Dateien: BPMN-Seite/Surface/Interop; `StudioUiContractTests.cs`, `BpmnEditorInsertionTests.cs`, `LocalStudioInfrastructureTests.EditorFeatures.cs`, `.EditorCorrections.cs`, `StudioVisualBaselineTests.cs`.

Abnahme: versionierte Inventur mit vorhandenen, fehlenden und ungeprüften Funktionen; lokale Artefaktpfade/Commit und Testzahlen. Keine Neuentwicklung vorhandener Features. Neue Findings separat priorisieren.

### W02 – Verträge vor Implementierung

- [ ] Dokumentidentität, lokale und serverseitige Revision, Kontextwechsel und State-Übergänge aus Abschnitt 4 konkret festlegen.
- [ ] Vorhandene Backend-Entwurfskomponenten verwenden, falls vorhanden. Andernfalls neuen Draft-Vertrag spezifizieren: ID, Tenant, Eigentümer/Zugriffsmodell, Modellart, Name, Inhalt, Revision, Basisdeployment und Zeitstempel. Deploymentversion und Draftrevision strikt unterscheiden.
- [ ] Vorgeschlagene API: Create/List/Get/Update/Delete für Drafts; Route und DTOs erst nach Inventur festlegen. Updates mit Pflicht-Revision/If-Match, atomarem DB-Vergleich und definiertem 409/412-Konflikt; keine read-then-unconditional-write-Sequenz.
- [ ] Rollenmatrix mit bestehenden Policies abstimmen: Lesen, Erstellen/Ändern, Löschen, Deployen; explizite Regeln für ReadOnly, ProcessManager und Admin sowie fremde/fehlende Tenant-Claims. Keine Standardtenant-Fallbacks.
- [ ] Authentifizierte Draft-Antworten, Größen-/XML-Limits, Redaktions-/Secret-Policy, Cache/Logging und Löschung/Aufbewahrung festlegen. DB-Migration für bestehende Installationen planen; kein zweiter ad-hoc Store.
- [ ] Bestehende gültige XML-Namespaces, Credential-Referenzen und Backend-Feldsemantik erfassen. Nicht unterstützte Ausführung eindeutig als Diagnose behandeln, nicht stillschweigend konvertieren.

Abnahme: konkreter Vertrag mit Beispielen für Erfolg, Konflikt, Zugriff verweigert, ungültige Eingabe und Save-vs-Deploy; Backend-/Security-Review vor W04. Wesentliche offene Produktentscheidungen dem Nutzer vorlegen, nicht durch den implementierenden Agent erfinden.

### W03 – Dokumentsitzung und Interop

- [ ] Kleine typisierte Interop-Fassade und Dokumentsitzung einführen/ergänzen; vorgeschlagene neue Namen `BpmnDocumentSession` und `BpmnModelerInterop` sind keine bereits existierenden C#-Typen.
- [ ] Command-Stack-Änderungen abonnieren, debouncte Snapshot-Erfassung und monotone Revisionen implementieren; nicht bei jedem Mausereignis das gesamte XML über den Blazor-Circuit übertragen.
- [ ] Save-/Import-/Validate-Operationen mit Dokumentgeneration korrelieren. Veraltete Antworten verwerfen. Auf Dispose Listener, Timer und .NET-/JS-Referenzen freigeben.
- [ ] XML-Übernahme, Import, Undo/Redo und Kontextwechsel an den Zustandsvertrag anbinden. Editor bei Werkzeugwechseln nicht neu mounten.
- [ ] Circuit-Unterbrechung explizit anzeigen. Nach Reconnect nicht automatisch einen älteren Server-Snapshot über das Browsermodell importieren.

Abnahme: T02/T03/T04/T12 aus Abschnitt 6; reproduzierbarer verspäteter Save/Import überschreibt keine neuere Arbeit; keine verdoppelten Listener nach 20 Öffnen/Schließen-Zyklen. Bestehende Einfüge-/Undo-Tests bleiben grün.

### W04 – Persistente Entwürfe und Konflikte

- [ ] W02-Vertrag end-to-end implementieren: DB/Migration → Repository/Service → autorisierte API → Studio. Tests mit echter Datenbank, nicht nur InMemory-Provider.
- [ ] Separaten „Entwurf speichern“-Befehl und Status einführen. Ungültige Ausführungslogik darf gespeichert werden, ohne Deployment oder Prozessstart auszulösen.
- [ ] Wiederherstellung aus bestätigter Serverrevision anbieten; gespeicherte Revision und gegebenenfalls lokale ungesicherte Änderungen unterscheiden.
- [ ] Zwei Tabs/Benutzer: Konflikt sichtbar melden; Optionen „Serverstand laden“, „Eigene Änderungen als separaten Entwurf sichern“, „Abbrechen“. Kein automatisches XML-Merge und kein stilles Last-write-wins.
- [ ] Fehlgeschlagener Save lässt dirty bestehen. Antwortverlust nach erfolgreichem DB-Commit durch Revision/Inhalt-Abgleich behandeln; keine blinde Wiederholung mit unerklärlichen Dubletten. Für Create eine korrelierbare/idempotente Anfrage vorsehen.
- [ ] Liste/Öffnen/Löschen mit Tenant-/Eigentumsprüfung und Größenlimits; Entwurfslöschung darf deployte Versionen nicht löschen.

Abnahme: T01–T05, T09 und T12 mit echter lokaler API/PostgreSQL; Migration von bestehendem DB-Stand; Fremdtenant-Tests für jede neue Operation. Anzahl der Deployments und Runtime-Instanzen bleibt beim Speichern unverändert.

### W05 – Vertex-Properties und gemeinsame Editorfunktionen

- [ ] `vertex.json`, Properties, Einfügefunktionen und Clientvalidierung hinter einer gemeinsamen internen Moduleinstiegsstelle bündeln; vorgeschlagen unter `tools/bpmn-io/src/vertex-editor/`. Keine npm-Veröffentlichung und kein Camunda-Plugin in diesem Paket.
- [ ] Feldmatrix für Task/Gateway/Event/Subprozess erstellen: UI-Feld → XML-Repräsentation → Backend-Auswertung → Test. Vorhandene Felder verbessern; keine UI-Felder anbieten, deren Wirkung in der Engine fehlt.
- [ ] Kontextabhängige Gruppen mit verständlichen Beschriftungen und Hilfen: allgemein, Ausführung, Eingabe/Ausgabe, Fehler/Retry, Referenzen. Advanced-XML bleibt zugänglich.
- [ ] Credential-, Decision-, Formular- und Connector-Auswahl an bestehende tenantgebundene Dienste anbinden. Nur Referenzen speichern; keine Secrets laden oder anzeigen.
- [ ] Alle Properties-Änderungen über den Command-Stack ausführen. Katalogeinfügen bleibt atomar undo-/redo-fähig; Default-Flows und Bedingungen erhalten.

Abnahme: T06; jedes neu angebotene Feld hat XML-Roundtrip und einen Nachweis der Backendwirkung oder eine explizite Nur-Dokumentation-Kennzeichnung. Fehlende Berechtigungen, leere Listen und API-Ausfälle ergeben verständliche Zustände statt verschwundener Konfiguration.

### W06 – Diagnosen, Kompatibilität und Roundtrip

- [ ] Gemeinsames Diagnoseformat definieren: Code, Schweregrad, Quelle, Element-ID, Nachricht und optionale Feldzuordnung; Client-/Server-Meldungen deduplizieren und an die geprüfte Revision binden.
- [ ] Schnelle Clientvalidierung debouncen. Serverseitige Validierung ohne Deployment bereitstellen, falls noch nicht vorhanden; vorhandenen Validator wiederverwenden und Zugriff/Größe absichern.
- [ ] Klick auf Diagnose selektiert/zentriert das Element und öffnet passende Properties. XML ohne auflösbare Element-ID erhält eine Dokumentdiagnose, keine erfundene Position.
- [ ] Eigene, Standard- und fremde XML-Erweiterungen mit echten Fixtures prüfen. Nicht verlustfrei importierbare Inhalte: Original verfügbar halten, Warnung anzeigen und destruktives Überschreiben blockieren. Ein unbekannter Namespace darf nicht still verloren gehen.
- [ ] Export-Redaktion respektieren: redigierte API-Exporte als solche anzeigen und nicht als unverändert ausführbaren Ausgangsstand deklarieren. Keine Rekonstruktion fehlender Secrets.

Abnahme: T07/T08; unveränderte Semantik nach Import → Edit → Export → Reimport nachweisen (IDs, Verknüpfungen, DI, Conditions, Referenzen, Erweiterungen), nicht ausschließlich XML-Stringgleichheit. Parserfehler und fehlende Validator-Bundles führen niemals zu „gültig“. Vollständige Camunda-/BPMN-Konformität wird dadurch nicht behauptet.

### W07 – Modelle, Versionen und Deployment

- [ ] Workspace-Navigation für Entwürfe und deployte Versionen mit Suche/Paging; Quelle, Name, Kontext und Revision sichtbar halten.
- [ ] Deployte Version beim Bearbeiten als neuen/zugeordneten Entwurf öffnen, nicht historische Version mutieren. Vorhandenen Versionsvergleich weiterverwenden und auf nachvollziehbare Unterschiede prüfen.
- [ ] Deploymentdialog zeigt Engine/Ziel, Tenant, Modell und Snapshotrevision. Deployment nur nach Serverprüfung; Doppelklick und parallele UI-Aufrufe kontrollieren.
- [ ] Antwortverlust nach Deployment als „Ergebnis unklar“ behandeln und vorhandene Deploymentdaten abgleichen; keinen automatischen zweiten Deploy anstoßen. Idempotenz nur behaupten, wenn serverseitig belegt.
- [ ] Neue Deployment-ID/Version und strukturierte Fehler anzeigen. Webhook-Secrets ausschließlich im bestehenden Einmal-Anzeigepfad behandeln, nicht in Draft/Logs kopieren.
- [ ] „Lokale Simulation“ und „Deploy and run test“ bleiben eindeutig getrennt; letzteres erzeugt reale persistente Artefakte und muss dies weiterhin erklären.

Abnahme: T10/T11; persistierte Version nach UI-Reload mit dem deployten Snapshot vergleichen; späteres Editieren verändert weder diese Version noch das angezeigte Deploymentergebnis.

### W08 – Workspace-UX und Zugänglichkeit

- [ ] Vorhandene Shell/Tokens nutzen: links einklappbare Modellnavigation, Mitte Zeichenfläche, rechts Properties, unten Diagnosen/Expertenwerkzeuge; oben Dokumentstatus und getrennte Save-/Deploy-Aktionen.
- [ ] Kleine visuelle Vorschau mit realistischen Fehler-, Leer-, Lade-, Konflikt- und Offlinezuständen erstellen; vor breitem Umbau abstimmen. Keine neue globale Gestaltung anderer Studioseiten.
- [ ] Tastaturaktionen: Speichern im Editor-Kontext, Undo/Redo ohne Überschreiben des Textfeld-Undo, Escape für Dialoge; sichtbarer Fokus und Fokus-Rückgabe nach Dialogen.
- [ ] Bei schmalem Viewport Panels als erreichbare Overlays/Sheets, Zeichenfläche nicht unbedienbar verkleinern. Keine pauschale Behauptung vollständiger mobiler Diagrammbearbeitung.
- [ ] Existierende Tabs, Katalog, Versionsvergleich, Viewer, XML und Simulations-/Testaktionen erhalten. Tooltips ergänzen, nicht als Ersatz für zugängliche Namen verwenden.

Abnahme: T13/T14; Screenshots 1440×900, 1024×768, 390×844; kein unbeabsichtigter horizontaler Seitenoverflow oder verdeckte Hauptaktionen, keine reine Farbcodierung, Bedienung ohne Maus für Navigation/Dialoge/Properties. Diagramm-Tastaturgrenzen separat dokumentieren; automatischer Scan ist keine vollständige Accessibility-Zertifizierung.

### W09 – Lokale Gesamtabnahme

- [ ] Alle T01–T14 ausführen und Ergebnis pro Fall festhalten; fehlende Pflichtfälle und Skips sind keine bestandene Abnahme.
- [ ] Vorhandene Kern-, Editor-, bpmn-moddle-/Validierungs-, UI-Vertrags- und lokale reale E2E-Regressionen ausführen. Andere Modellierer und Studio-Navigation mitprüfen.
- [ ] Nach den letzten Änderungen und Commit den finalen Kandidaten aus sauberem Checkout nachqualifizieren; vor Commit bleibt der Bericht explizit ein Dirty-Stand.
- [ ] Bericht mit Commit, Testnamen, Modus, Infrastruktur, Zahlen, Dauer, Logs/Screenshots, Warnungen und bekannten Grenzen ablegen. Rohartefakte lokal lassen und vor Weitergabe prüfen.
- [ ] Benutzeranleitung für Draft/Save, Konflikte, Wiederherstellung, XML-Grenzen und Deployment aktualisieren. Migration und sichere Rückkehr zum vorherigen Stand dokumentieren; kein Downgrade-Schema ohne Nachweis versprechen.

Abnahme: alle Muss-Kriterien erfüllt oder ausdrücklich als offen markiert. Kein „fertig“ bei ungeprüfter Funktion; keine CI-Workflow-Änderung. Produktionsfreigabe bleibt an den separaten Produktionsqualitätsplan gebunden.

### W10 – Bewusst nicht Teil des ersten Lieferumfangs

- Gemeinsames Editorpaket in einem optionalen Camunda-Desktop-Plugin verwenden.
- Mehrere gleichzeitig geöffnete Dokumenttabs, Echtzeit-Kollaboration/CRDT, automatisches semantisches Merge.
- Lokales dauerhaftes Offline-Autosave nach abgestimmtem Sicherheits-/Aufbewahrungskonzept.
- Breiter DMN-/CMMN-/Form-Workspace-Ausbau, neue Marketplace-/KI-Funktionen.

Diese Punkte benötigen eigene Aufträge; sie dürfen W01–W09 nicht unbemerkt vergrößern.

## 6. Verbindliche Abnahmeszenarien

Die Test-IDs sind geplante Fälle, keine Behauptung bereits existierender Testmethoden. In W01 auf bestehende Methoden abbilden und nur Lücken ergänzen. Tests verwenden deterministische Synchronisationspunkte/steuerbare Antworten statt beliebiger Wartezeiten.

| ID | Konkreter Ablauf | Erwarteter Nachweis |
|---|---|---|
| T01 | Neues BPMN bearbeiten, Entwurf speichern, Studio neu öffnen | Dasselbe Modell aus echter Persistenz; keine neue Deployment-/Runtime-Instanz. |
| T02 | Speichern von Revision N verzögern, währenddessen N+1 bearbeiten | Antwort für N setzt N+1 nicht auf gespeichert; erneutes Save persistiert N+1. |
| T03 | Import/Save starten, Dokument oder Tenant wechseln, alte Antwort freigeben | Kein Überschreiben des neuen Dokuments; alter Tenantinhalt erscheint nicht im neuen Kontext. |
| T04 | Properties/Katalog ändern, Undo/Redo, Panelwechsel, fehlerhaftes XML übernehmen | Graph und Erweiterungen korrekt; kein Modellverlust durch Panelwechsel oder fehlgeschlagenen Import. |
| T05 | Zwei Sitzungen öffnen dieselbe Serverrevision und speichern abweichend | Ein Update gewinnt, das andere erhält Konflikt; keine verlorenen Änderungen, Kopie-/Abbrechen-Pfad funktioniert. |
| T06 | Service-Task konfigurieren, Credential-/Decision-Referenz wählen, Save/Reload | Korrekte XML-Referenzen und Backendwirkung; keine Secretwerte im Response oder Log. |
| T07 | Fehlende Bedingung und defekten Scope erzeugen, Client/Server prüfen, Diagnose anklicken | Fehler am richtigen Element; Draft möglich, Deployment gesperrt; veraltete Diagnose überschreibt keine neue. |
| T08 | Reale Vertex- und Fremd-BPMN-Fixtures importieren, ändern, exportieren, reimportieren | Semantischer Vergleich einschließlich DI/Extensions; Verlustfall erhält Original und blockiert Überschreiben. |
| T09 | ReadOnly/Fremdtenant/fehlender Claim greifen auf jede Draft-Operation zu | Vereinbarte 401/403/404, keine DB-Nebenwirkung und keine fremden Daten; UI-Verstecken allein genügt nicht. |
| T10 | Gespeicherten Draft deployen, Version laden/vergleichen, weiter editieren | Exakt deployter Snapshot bleibt unverändert; neue Änderungen erkennbar ungespeichert. |
| T11 | Deploy-Doppelklick und verlorene Antwort nach erfolgreichem Server-Commit | Kein unkontrolliertes automatisches Zweitdeployment; unklarer Status wird aufgelöst oder ehrlich angezeigt. |
| T12 | Save-Fehler, DB-/Netzunterbrechung, Circuit-Reconnect und Browser-Neustart | Keine falsche Speicherbestätigung; nach Neustart letzter bestätigter Serverstand, kein versprochener ungesicherter Stand. |
| T13 | Navigation, Properties, Dialoge und Fehlerliste per Tastatur auf drei Viewports | Fokusführung und erreichbare Aktionen; dokumentierte Canvas-Grenzen, Screenshots geprüft. |
| T14 | 100/500/1.000-Element-Fixtures öffnen/editieren sowie 20 Mount/Dispose-Zyklen | Import-/Eingabelatenz, Requests und Listener messen; keine Duplikate/Leaks. In W01 Zielwerte anhand Testhardware festlegen, nicht nachträglich an schlechte Ergebnisse anpassen. |

Zusätzlicher Sicherheitsfall für W04/W06: XML mit DTD/externen Entitäten, übergroße Eingabe und erkannte Inline-Secrets dürfen keine Serverdateien lesen, Limits umgehen oder unredigierte Exporte ermöglichen. Redaktion darf nicht still einen erfolgreich gespeicherten Originalzustand vortäuschen.

## 7. Testwerkzeuge und Ausführung

Vor jedem Lauf Voraussetzungen/Parameter am aktuellen Checkout prüfen. Keine Zugangsdaten in dieses Dokument eintragen.

### Vorhandene JavaScript-Prüfungen

Im Verzeichnis `src/VertexBPMN.Studio`:

```powershell
npm run test:vertex-moddle
npm run test:flow-validation
npm run build:bpmnio
```

Bei fehlenden Dependencies den vorhandenen Lockfile-basierten Installationsweg nutzen. Neue Tests über die Paket-Skripte reproduzierbar erreichbar machen. Für geänderte Editorquellen nicht mit alten Bundles testen.

### Reale lokale GUI-E2E

Vom Repository-Root, abhängig von tatsächlich verfügbarer Infrastruktur:

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Existing
# Alternative für bewusst bereitgestellte WSLC-Testdienste:
./scripts/test-studio-e2e.ps1 -Infrastructure Wslc
```

Host/Ports und Credentials über unterstützte Parameter/Umgebung konfigurieren. Vor Wslc-Modus die Ressourcenwirkung des Skripts prüfen und keine Benutzerinstanzen ersetzen. `-TestMethod` kann vorhandene Methoden gezielt auswählen; null entdeckte Tests oder übersprungene Pflichtfälle führen zum Fehler. `-SkipBuild` nur bei nachweislich aktuellen Artefakten, nicht für die finale Abnahme.

`StudioUiTestHost`/UI-Vertragstests nicht mit `LocalStudioE2ETestHost` und echter API/DB gleichsetzen. Für den finalen gebündelten Nachweis außerdem den bestehenden Einstieg `scripts/test-production-readiness.ps1` und dessen Runbook prüfen; Parameter nicht aus diesem Plan erraten. GUI bleibt lokal und wird nicht in CI aufgenommen.

## 8. Übergabeformat für Menschen und Modelle

Pro Paket eine kurze Datei unter `docs/reviews/` oder einen klar getrennten Abschnitt im Fortschrittsbericht führen:

```markdown
## Wxx – Titel
- Status: offen / in Arbeit / blockiert / implementiert, nicht abgenommen / abgenommen
- Basiscommit und Branch:
- Geänderte Dateien:
- Entscheidung und begründete Abweichung vom Plan:
- Reproduktion vor dem Fix:
- Implementierung und Auswirkungen auf Verträge/Migration:
- Tests: exakte Befehle, Methoden, Modus, passed/failed/skipped, Artefaktpfade
- Sicherheits-/Kompatibilitätsgrenzen:
- Offene Punkte und benötigte Nutzerentscheidung:
- Nächster sicherer Arbeitsschritt:
```

Kopierbarer Startauftrag:

> Lies `docs/reviews/2026-09-09_Studio-Modellierungsworkspace_Implementierungsplan.md` vollständig und prüfe den aktuellen Repository-Stand. Bearbeite ausschließlich das nächste freigegebene W-Paket unter Beachtung seiner Abhängigkeiten. Prüfe vorhandene Implementierung und Tests, bevor du neue Funktionen anlegst. Erhalte Engine-Semantik, Tenant-/Rollenschutz, XML-Erweiterungen und alle bisherigen Editoraktionen. Beweise Änderungen mit den vorgesehenen lokalen Tests; keine Tests abschwächen oder Persistenz durch Mocks ersetzen. Aktualisiere den Paketstatus mit überprüfbaren Nachweisen. Bei fehlender Entscheidung konkret nachfragen; keinen Erfolg erfinden und keine späteren Pakete ungefragt beginnen. Kein Commit/Push ohne gesonderten Auftrag.

Für kleinere Modelle: jeweils ein klar begrenztes Teilziel innerhalb eines W-Pakets übergeben, insbesondere Tests und begrenzte UI-/Interop-Änderungen. W02, DB-Konkurrenz, Kontext-/Security-Grenzen und die finale Abnahme benötigen einen separaten Review. Modellgröße allein ist kein Qualitätsnachweis.

## 9. Fortschritt und erster Schritt

- [x] Plan erstellt, mit vorhandenen Einstiegspunkten und historischen Plänen abgeglichen.
- [ ] W01 – Baseline und vollständige Funktionsinventur.
- [ ] W02 – Verträge und offene Produktentscheidungen.
- [ ] W03 – Dokumentsitzung und Interop.
- [ ] W04 – Entwurfspersistenz und Konflikte.
- [ ] W05 – Vertex-Properties und gemeinsame Module.
- [ ] W06 – Diagnosen und Roundtrip.
- [ ] W07 – Navigation, Versionen und Deployment.
- [ ] W08 – Workspace-UX und Zugänglichkeit.
- [ ] W09 – Finale lokale Abnahme.

**Nächster freizugebender Umsetzungsschritt: W01.** Noch keine parallelen Änderungen an BPMN-Seite, Interop oder Datenbankschema starten. W10 bleibt optional und zählt nicht zum Abschluss von W01–W09.
