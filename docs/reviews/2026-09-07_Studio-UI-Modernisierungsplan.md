# Studio UI: Analyse und Modernisierungsplan

Stand: 2026-09-08. Status: Phase 0 bis Phase 7 abgeschlossen.

Fortschritt:

- [x] Phase 0 – Bestandsaufnahme und Funktionsschutz. Ergebnisse: [Studio-UI-Phase-0-Baseline](2026-09-07_Studio-UI-Phase-0-Baseline.md)
- [x] Phase 1 – Designfreigabe. Ergebnisse: [Studio-UI-Phase-1-Designfreigabe](2026-09-07_Studio-UI-Phase-1-Designfreigabe.md)
- [x] Phase 2 – Designsystem und Shell. Ergebnisse: [Studio-UI-Phase-2-Designsystem-und-Shell](2026-09-07_Studio-UI-Phase-2-Designsystem-und-Shell.md)
- [x] Phase 3 – BPMN-Pilot. Ergebnisse: [Studio-UI-Phase-3-BPMN-Pilot](2026-09-07_Studio-UI-Phase-3-BPMN-Pilot.md)
- [x] Phase 4 – Weitere Modellierer. Ergebnisse: [Studio-UI-Phase-4-Weitere-Modellierer](2026-09-08_Studio-UI-Phase-4-Weitere-Modellierer.md)
- [x] Phase 5 – Operative Kernseiten. Ergebnisse: [Studio-UI-Phase-5-Operative-Kernseiten](2026-09-08_Studio-UI-Phase-5-Operative-Kernseiten.md)
- [x] Phase 6 – Restliche Seiten. Ergebnisse: [Studio-UI-Phase-6-Restliche-Seiten](2026-09-08_Studio-UI-Phase-6-Restliche-Seiten.md)
- [x] Phase 7 – Lokale Gesamtabnahme. Ergebnisse: [Studio-UI-Phase-7-Lokale-Gesamtabnahme](2026-09-08_Studio-UI-Phase-7-Lokale-Gesamtabnahme.md)

## 1. Ziel und unveränderliche Grenzen

VertexBPMN Studio soll wie ein konsistentes professionelles Prozesswerkzeug wirken: ruhig, hochwertig, schnell erfassbar und effizient bedienbar. Nicht möglichst viele Effekte, sondern klare Orientierung und ausreichend Arbeitsfläche.

Unverändert bleiben fachliche Funktionen, API-Verträge, Berechtigungen, Tenant-/Engine-Kontext, Routen und Deep Links, Validierung, Import/Export, Prozessausführung und Datenpersistenz. Bestehende Aktionen dürfen neu angeordnet, aber nicht entfernt, stillschweigend zusammengeführt oder semantisch umbenannt werden. Insbesondere ist „Deploy“ kein gewöhnliches „Speichern“.

Keine Änderung an ProcessEngine, Domain, API oder Authentifizierungslogik für dieses Redesign. Kein Wechsel des Blazor-Rendermodus, keine neue Komponentenbibliothek und kein Paketupgrade als versteckter Bestandteil. Gefundene Funktionsfehler werden separat erfasst.

## 2. Gesicherte Ausgangslage

Codebasierte Analyse der Shell, Navigation, globalen Styles, BPMN-Seite und repräsentativer Dashboard-/Listenansichten. Kein neuer Build und kein Live-Browser-Audit für diese Planung. Aussagen über tatsächliche Pixelabstände, Kontrast und Nutzerverhalten müssen in Phase 0 bestätigt werden.

| Befund | Codebeleg | Konsequenz / Bewertung |
|---|---|---|
| .NET 10, InteractiveServer und MudBlazor 9.9.0 vorhanden | `src/VertexBPMN.Studio/VertexBPMN.Studio.csproj`, `Components/App.razor:31` | Tragfähige Basis; kein Frameworkwechsel für modernes UI nötig. |
| Unkonfiguriertes `new MudTheme()` | `Components/Layout/MainLayout.razor:80` | Bisher kein dort definiertes produktspezifisches Theme. |
| Alle Seiten in `MaxWidth.Large`, zusätzlich `my-16 pt-16` | `Components/Layout/MainLayout.razor:73` | Formularseite und grafischer Editor erhalten denselben Platzrahmen; Abstände am Bildschirm prüfen. |
| 28 flache Navigationslinks, CMMN capabilityabhängig | `Components/Layout/NavMenu.razor` | Keine sichtbare Gruppierung nach Arbeitsaufgabe. |
| Bootstrap zusätzlich zu MudBlazor; Roboto geladen, global Helvetica gesetzt | `Components/App.razor:8`, `:10`; `wwwroot/app.css:2` | Mehrere Stilquellen; konkrete Kaskadenkonflikte erst messen, nicht pauschal unterstellen. |
| Editor mit 320-px-Properties-Spalte und 560-px-Mindesthöhe | `wwwroot/app.css:62` | Starre Geometrie; adaptive Arbeitsfläche fehlt in diesen Regeln. |
| BPMN-Werkzeugleiste, technischer Einführungstext, XML, Versionen und zusätzlicher Viewer auf derselben Seite | `Components/Pages/BpmnModelerPage.razor` | Arbeitsaufgabe und Diagnoseinformationen konkurrieren um Aufmerksamkeit. |
| Unterschiedliche Titelgrößen, Tabellendichte und Zustandsdarstellung | `Components/Pages/Home.razor`, `Tasks.razor`, `ProcessInstances.razor` | Gemeinsame Seiten- und Tabellenkonventionen sinnvoll. |
| Layout-CSS enthält `.page`, `.sidebar`, `.top-row`, Shell verwendet MudLayout | `Components/Layout/MainLayout.razor.css` und `.razor` | Kandidaten für Template-Altlasten; vor Entfernung Verwendungen prüfen. |
| Sämtliche Editorbibliotheken global geladen | `Components/App.razor` | Initiallast messen; Lazy Loading wäre eine separate, risikobehaftete Optimierung und ist kein erster Redesign-Schritt. |

Pfade ohne Präfix in dieser Tabelle sind relativ zu `src/VertexBPMN.Studio/`.

## 3. Technische Entscheidung: eigenes Designsystem auf MudBlazor

Empfehlung: vorhandenes MudBlazor durch ein explizites `MudTheme`, semantische CSS-Variablen und kleine wiederverwendbare Razor-Komponenten gestalten. MudBlazor unterstützt Theme-, Class- und Style-Anpassungen offiziell: [Theming](https://mudblazor.com/customization/overview), [Default Theme](https://mudblazor.com/customization/default-theme).

| Option | Nutzen | Nachteil für dieses Projekt | Entscheidung |
|---|---|---|---|
| MudBlazor + eigenes Theme | Vorhandene Controls, Bindings und Dialoge bleiben; eigenes Erscheinungsbild möglich | Erfordert echte Designarbeit, nicht nur neue Primärfarbe | Empfohlen |
| Fluent UI Blazor | Alternative für eine Microsoft-nahe Oberfläche | Austausch von Controls und Interaktionsverhalten; neue Regressionen | Kein Wechsel im Redesign |
| Radzen | Weitere Komponenten und Themevarianten | Migration ohne belegten Funktionsbedarf; Theme-Lizenzumfang separat prüfen | Kein Wechsel im Redesign |
| Eigene Controls / Utility-CSS als Ersatz | Maximale Gestaltungsfreiheit | Fokussteuerung, Validierung und komplexe Controls selbst pflegen | Nur eigene Layoutbausteine, keine Neuentwicklung aller Controls |

Alternativen: [Fluent UI Blazor](https://fluentui-blazor.azurewebsites.net/), [Radzen Themes](https://blazor.radzen.com/themes). Diese Entscheidung ist eine projektbezogene Abwägung, kein allgemeines Qualitätsranking.

## 4. Gestalterisches Zielbild

### Visuelle Sprache

- Helles, neutral getöntes Arbeitsumfeld; weiße Arbeitsflächen, dunkle gut lesbare Texte, eine ruhige blaue Akzentfarbe. Farbwerte werden als Vorschlag in Prototypen geprüft, nicht vorab als barrierefrei bezeichnet.
- Orientierung für Tokens: Hintergrund `#F6F8FB`, Fläche `#FFFFFF`, Text `#172033`, Akzent `#2563EB`. Semantische Tokens statt verstreuter Hexwerte.
- Einheitliche lokal verfügbare Systemschrift, z. B. Segoe UI mit system-ui-Fallback; 14–16 px für Arbeitsinhalte, klar abgestufte Überschriften. Keine neue externe Font-Abhängigkeit.
- 4/8-px-Abstandsraster, ca. 8-px-Ecken, sparsame Schatten, feine Trennlinien. Farbe signalisiert Zustand oder primäre Handlung, nicht Dekoration jeder Karte.
- Text plus Icon für wichtige Aktionen; Icon-only nur mit zugänglichem Namen und Tooltip. Status nicht allein über Rot/Grün vermitteln.
- Kurze Übergänge nur für Orientierung, `prefers-reduced-motion` respektieren. Kein Glassmorphism oder stark animierter Hintergrund im Arbeitsbereich.
- Zuerst ein vollständig abgestimmtes Light Theme. Dark Mode ist optional und separat abzunehmen, besonders für bpmn.io, DMN, CMMN und Formulare; kein pauschaler CSS-Invert-Filter.

### Shell und Navigation

Eine kompakte Kopfzeile für Produkt und bestehenden Tenant-/Engine-Kontext. Tenant und Engine bleiben sichtbar unterscheidbar; kein nur farblich codierter Kontext. Hauptinhalt erhält eine klare Seitenüberschrift und bestehende Aktionen. Links nach Aufgaben gruppieren, Routen beibehalten:

| Bereich | Bestehende Ziele |
|---|---|
| Übersicht | Dashboard |
| Modellieren | BPMN, DMN, CMMN, Form Builder, Process Definitions |
| Arbeiten und Betreiben | Tasks, Process Instances, Deployments, Workflow Triggers, Messages & Signals |
| Analysieren | History, Execution Details, Event Log, Analytics, Performance, Health, Simulation, Debugging, Compliance |
| Verwalten | Engine Management, Tenants, Credentials, Configuration, Feature Flags, Migration, Connectors, Extensions, SSO |

Capabilityabhängigkeit von CMMN bleibt erhalten. Keine neue rollenbasierte Ausblendung erfinden. Auf kleinen Viewports wird die Navigation zum Overlay, nicht zu einer zweiten vertikalen Vollbreitenspalte.

### Drei Layouttypen statt eines Universalcontainers

1. Arbeitsbereich: BPMN/DMN/CMMN/Form Builder nutzen verfügbare Breite und Höhe.
2. Datenansicht: Listen und Analysen fluid mit sinnvoll begrenzter Textbreite, kontrolliertem Tabellenoverflow.
3. Formular-/Konfigurationsseite: lesbare Maximalbreite, übersichtliche Abschnitte.

### Modellierer als zentraler Pilot

- Kompakte Toolbar mit derselben Aktionssemantik. Import/Export gruppieren; Deploy bleibt explizit Deploy. Häufige Funktionen nicht unnötig hinter Menüs verstecken.
- Canvas als Hauptarbeitsfläche; Properties rechts, größenveränderbar mit Tastaturalternative und Mindestbreite.
- Bestehende XML-Vorschau, Validierung, Versionsvergleich und Viewer in klar benannten Panels/Tabs bündeln. Alle Funktionen bleiben erreichbar.
- Panelwechsel dürfen die Modeler-Instanz nicht neu erzeugen: Auswahl, Zoom, ungespeicherte Änderungen und Undo-Stack erhalten. Layoutänderung muss Resize korrekt an JS-Editor melden, ohne Reimport des XML.
- Deploy-/Validierungsfehler öffnen den relevanten Bereich und behalten bestehende Fehlernavigation bei.
- Einmalig angezeigte Webhook-Secrets und Importwarnungen dürfen nicht durch geschlossene Panels oder automatische Toasts verschwinden.
- Keine Änderung an BPMN-DI-Geometrie oder Exportdaten nur für das Shell-Redesign. Keine Bearbeitung generierter `wwwroot/lib`-Dateien von Hand.

### Listen, Aufgaben und Dashboard

- Ein gemeinsames Seitenkopf-, Toolbar-, Status- und Tabellenbild; Aufgabenname und Status visuell vor technischen IDs, vollständige IDs bleiben zugänglich.
- Vorhandene Suche, Sortierung, Auswahl, Paging und Aktionen behalten exakt ihre Datenbedeutung. Neue Filter oder serverseitiges Paging sind nicht Bestandteil des Designs.
- Bestehende Task-Details/-Dialoge modernisieren, ohne Claim-/Complete-Bedingungen oder Formwerte zu verändern.
- Loading, leer, keine Suchtreffer, Fehler und fehlende Berechtigung unterscheiden, soweit entsprechende Zustände heute vorhanden sind. Keine neuen API-Anfragen allein zur Dekoration.
- Dashboard ordnet vorhandene Kennzahlen und Links neu; keine erfundenen KPI, Charts oder Daten.

## 5. Umsetzungsplan

Aufwand in Personentagen für eine erfahrene Blazor-Entwicklung einschließlich lokaler Prüfung, keine feste Lieferzusage. Phasen gelten als erledigt erst nach Abnahmekriterien.

| Phase | Aufgaben und konkrete Dateien | Abnahme | Aufwand | Abhängigkeit |
|---|---|---|---|---|
| 0 – Bestandsaufnahme und Funktionsschutz | Alle `Components/Pages/*.razor` und Dialoge inventarisieren; Route → Aktion → Handler → Ergebnis dokumentieren. Echten Studio-Stand mit Testdaten fotografieren, bestehende lokale UI-Tests ausführen; aktuelles CSS/Overflow messen. | Jede bestehende Aktion in Paritätsmatrix, Baseline und bekannte Fehler getrennt dokumentiert. | 2–3 | Keine |
| 1 – Designfreigabe | Drei anschauliche Entwürfe derselben Designsprache: Shell/Dashboard, BPMN-Arbeitsbereich, Tasks. Desktop und schmale Ansicht, jeweils mit Daten/Fehlerzustand. Keine Anbindung oder Produktänderung. | Nutzer bestätigt konkrete Screens, Typografie, Dichte, Navigation und Aktionsanordnung. | 2–3 | 0 |
| 2 – Designsystem und Shell | Neu: `Styling/StudioTheme.cs`, `wwwroot/css/studio-tokens.css`, kleine `Components/Shared`-Bausteine für PageHeader, Toolbar und StatePanel. `MainLayout`, `NavMenu`, `App.razor`, `app.css` anpassen. Bootstrap-Nutzung vor selektiver Entfernung inventarisieren. | Jede Route erreichbar; Kontextwechsel wie vorher; keine CSS-/Dialog-/Popover-Kollisionen; responsive Shell. | 3–5 | 1 |
| 3 – BPMN-Pilot | `BpmnModelerPage.razor`, `BpmnModelerSurface.razor` und gezielte Shell-CSS-Regeln modernisieren. Bestehende Handler anbinden; keine fachliche Logik neu schreiben. | Import, Edit, Export, Validate, Deploy, Versionen, n8n, Webhooks, Engine-Test und Undo/Redo unverändert; Panelwechsel verliert keinen Zustand. | 4–6 | 2 |
| 4 – Weitere Modellierer | `DmnModelerPage`, `CmmnModelerPage`, `FormBuilderPage` und jeweilige Surfaces in das gemeinsame Arbeitslayout übertragen. Editorspezifische Tabellen/Properties erhalten. | Jeder vorhandene Use Case pro Editor läuft; keine pauschale Übertragung BPMN-spezifischer Controls. | 3–5 | 3 |
| 5 – Operative Kernseiten | `Home`, `Tasks`, `ProcessDefinitions`, `ProcessInstances`, `Deployments`, `History`, `ExecutionDetails` einschließlich Dialoge vereinheitlichen. | Paritätsmatrix grün; gleiche Backend-Ergebnisse, Formwerte, Berechtigungen und Deep Links. | 4–6 | 2, Pilotfreigabe |
| 6 – Restliche Seiten | Alle übrigen Seiten aus Phase 0 nach Daten-/Formular-/Analysevorlage migrieren; auch Error-/Fallback-Seiten. Counter/Demoseiten inventarisieren, nicht ungefragt löschen. | Kein produktiver Bildschirm bleibt im alten Stil; jede Aktion bleibt vorhanden. | 4–7 | 4, 5 |
| 7 – Lokale Gesamtabnahme | Screenshotvergleich, reale E2E, Tastatur/Screenreader-Stichproben, Zoom/Reflow, Messung von Layout und Antwortverhalten, Rest-CSS aufräumen. | Kriterien aus Abschnitt 6 erfüllt; Abweichungen sichtbar dokumentiert statt Tests abgeschwächt. | 3–5 | 6 |

Gesamt: **25–40 Personentage**, grob **5–8 Arbeitswochen** für eine Person. Nach Phase 0 anhand der vollständigen Dialog-/Seitenmatrix nachschätzen. Dark Mode, globale Command-Palette, neue Tastaturkommandos, Personalisierung und neue Datenfunktionen sind nicht eingerechnet.

## 6. Schutz vor funktionalen Regressionen und messbare Abnahme

- Bestehende `Services/`, API-/Domain-Verträge und Interop-Verträge bleiben unverändert. Wiederverwendbare UI-Komponenten erhalten Parameter/Callbacks, keine versteckte Tenant- oder Workflowlogik.
- Semantische `data-testid` erhalten. Tests dürfen neue Tabs/Menüs öffnen, aber keine Geschäftsassertions entfernen. Bestehende E2E-Fälle in `tests/VertexBPMN.Studio.UiTests/` wiederverwenden und um fehlende Präsentationszustände ergänzen.
- Pro Route Screenshot in reproduzierbarem Zustand; 1440×900 und 1280×800 für Hauptarbeit, 1024×768 und 390×844 für Navigation/Reflow. Diagramme und große Datentabellen dürfen gezielt zweidimensional navigierbar sein; kein unkontrollierter horizontaler Seitenoverflow.
- Bei 1280×800 im BPMN-Arbeitslayout Ziel: mindestens 500 px Canvas-Höhe ohne Scrollen der gesamten Seite. Konkrete Grenzen nach Baseline festlegen.
- Vollständige Tastaturbedienung der Shell, Formulare und Aktionen; sichtbarer Fokus, Rückkehr zum Auslöser nach Dialogen, keine verdeckten fokussierten Controls. Einschränkungen der Diagramm-Editoren gesondert testen und dokumentieren, keine pauschale Accessibility-Zusage.
- WCAG 2.2 AA als Ziel: Textkontrast 4,5:1 für normalen bzw. 3:1 für großen Text; relevante Nichttext-Kontraste 3:1. Zielgrößen mindestens 24×24 CSS-Pixel bzw. zulässige Abstandsregel, nach Möglichkeit 40–44 px für häufige Controls. Automatisierte Checks plus manuelle Prüfung: [WCAG 2.2](https://www.w3.org/TR/WCAG22/).
- Gleiche Benutzeraktionen erzeugen gleiche fachliche Requests und Resultate. Gesperrte Aktionen bleiben gesperrt. Kein doppelter Submit, keine verlorene Datei, keine zusätzlichen automatischen Deployments.
- Ungespeichertes Modell, Zoom, Auswahl, Formularwerte und Undo/Redo bleiben bei reinen Layoutwechseln erhalten. Persistenz dieser Zustände über Navigation wird nicht neu versprochen.
- Performance vor/nachher in identischem lokalen Setup messen: Initialload, Tabwechsel und Texteingaben. Vorgeschlagenes Budget: höchstens 10 % Verschlechterung bei reproduzierbaren Messungen; keine zusätzlichen API-Roundtrips durch reine Darstellung. Komponenten nicht ungezielt zerlegen oder bei jeder Eingabe den Canvas neu rendern. [Microsoft: Blazor Rendering Performance](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0).
- Virtualisierung nur bei gemessenem Bedarf und nach separater Paritätsprüfung von Suche, Sortierung, Auswahl und Accessibility; nicht automatisch alle Tabellen austauschen.
- Tests laufen **lokal**, kein Ausbau der GitHub-Workflows. Reale E2E weiterhin mit WSLC und isolierten Datenbanken; Screenshots ohne produktive Secrets oder personenbezogene Daten.
- Pro Phase kleiner Review-/Commitumfang. Kein gleichzeitiges Fachrefactoring. Kein pauschales Ersetzen der gesamten Stylesheets oder Bibliotheksupdates.

## 7. Abschluss

Die sieben Phasen wurden umgesetzt und lokal abgenommen. Der belegte Abschlussstand einschließlich Screenshotmatrix, Accessibility-/Reflow-Prüfungen, Performancewerten und realen WSLC-E2E-Ergebnissen ist in [Phase 7](2026-09-08_Studio-UI-Phase-7-Lokale-Gesamtabnahme.md) dokumentiert. Einschränkungen externer Diagrammeditoren und außerhalb des Redesign-Scope beobachtete Backendprobleme werden dort ausdrücklich getrennt ausgewiesen; daraus wird keine pauschale Accessibility- oder Produktvollständigkeitszusage abgeleitet.
