# Studio UI – Phase 5: Operative Kernseiten

Stand: 2026-09-08. Status: abgeschlossen.

## 1. Umfang und Ergebnis

Die operativen Kernseiten `Home`, `Tasks`, `ProcessDefinitions`, `ProcessInstances`, `Deployments`, `History` und `ExecutionDetails` verwenden nun das gemeinsame Designsystem aus Phase 2. Die sechs zugehörigen Dialoge `StartProcessDialog`, `TaskDetailsDialog`, `ProcessInstanceDetailsDialog`, `ProcessInstanceHistoryDialog`, `ProcessVersionsDialog` und `BpmnViewerDialog` wurden ebenfalls vereinheitlicht.

Fachlogik, Services, API-Verträge, Routen, Berechtigungen, Tenant-/Engine-Kontext und Dialogrückgaben wurden nicht verändert. Eine im ersten Real-E2E-Lauf entdeckte Labelabweichung an `ExecutionDetails` wurde als Paritätsregression behandelt und rückgängig gemacht.

## 2. Umgesetzte UI-Konventionen

- Gemeinsamer `StudioPageHeader` mit Arbeitsbereich, Titel, Beschreibung und klarer Primäraktion.
- Einheitliche Datenflächen mit ruhiger Umrandung, kompakter Toolbar und responsiver Tabellen-/Kartenansicht.
- Fachliche Namen und Status stehen visuell vor technischen IDs; vollständige IDs bleiben lesbar und umbrechen sicher.
- Gemeinsame Lade-, Fehler- und Leerzustände über `StudioStatePanel`.
- Dashboard ohne erfundene Kennzahlen verdichtet; bestehende vier Metriken, Workload und letzte Tasks bleiben erhalten.
- Execution-Details-Abfragen in einer responsiven Toolbar; JSON-Ergebnisse sind begrenzt, scrollbar und mobil ohne Seitenoverflow.
- Dialoge verwenden dieselben Flächen, Abstände, technischen ID-Stile, Zustände und Aktionshierarchien wie die Seiten.
- `ProcessInstances` zeigt ab Desktopbreite eine echte Tabelle und wechselt erst unterhalb `Md` in die Kartenansicht.

## 3. Funktionsparität

| Bereich | Erhaltene Aktionen und Ergebnisse | Status |
|---|---|---|
| Dashboard | Laufzeitkennzahlen, Refresh, Navigation zu Tasks und Definitionen | grün |
| Tasks | Suche, Sortierung, Claim, Complete, Details/Formular | grün |
| Process Definitions | Suche, Filter, Gruppierung, Paging, BPMN-Viewer, Start, Versionen, Delete | grün |
| Process Instances | Suche, Sortierung, Paging, History, Details, Suspend, Resume, Delete, Incidentstatus | grün |
| Deployments | Auswahl mehrerer Dateien, Größen-/XML-Prüfung, Deployment, Ergebnisliste | grün |
| History | Laden, Refresh, sortierte persistierte Events | grün |
| Execution Details | Jobs, Incidents, Variablen und ID-Validierung | grün |
| Dialoge | Eingaben, Tabs, Viewer, Downloads, Rückgabewerte und Folgeaktionen | grün |

## 4. Verifikation

- Release-Build `VertexBPMN.Studio.UiTests` mit `SkipBpmnIoAssetBuild=true`: **0 Fehler**.
- `StudioUiContractTests`: **30/30 erfolgreich**. Fünf neue Verträge prüfen Primäraktionen und fehlenden horizontalen Overflow bei 390 × 844 px.
- Lokale Real-E2E-Szenarien gegen WSLC PostgreSQL und RabbitMQ: **5/5 erfolgreich**:
  - Dashboard und Process Definitions einschließlich Refresh, Paging, Versionen, Viewer und persistentem Löschen.
  - Process Instances und Tasks einschließlich Suche, Suspend/Resume/Delete und Complete.
  - Deployments einschließlich Größen-/XML-Validierung, Mehrfachupload und Sichtbarkeit in Process Definitions.
  - Execution Details einschließlich Jobs, Incidents und Variablen.
  - Runtime-Flow einschließlich Taskformular und persistierter History.
- `StudioVisualBaselineTests`: **1/1 erfolgreich**, 22 Screenshots für elf Seiten auf Desktop und Mobile; in allen Szenarien entspricht die Dokumentbreite exakt der Viewportbreite.
- Screenshots wurden visuell auf Hierarchie, responsive Tabellenkarten und Aktionsanordnung geprüft. Der dabei sichtbare zu frühe Kartenmodus von `ProcessInstances` wurde auf `Breakpoint.Md` korrigiert.

Bekannt bleiben die bereits vorhandene NU1900-Warnung bei nicht erreichbaren NuGet-Sicherheitsdaten sowie zwei xUnit2013-Analyzerwarnungen außerhalb dieses UI-Umfangs.

## 5. Abnahme

- [x] Sieben operative Kernseiten vereinheitlicht.
- [x] Sechs zugehörige Dialoge vereinheitlicht.
- [x] Keine Fachaktion oder stabile Feldsemantik entfernt.
- [x] Contract- und Real-E2E-Parität nachgewiesen.
- [x] Desktop und Mobile ohne horizontalen Seitenoverflow nachgewiesen.

Nächster Schritt ist Phase 6: die restlichen Analyse-, Verwaltungs- und Integrationsseiten auf dieselben UI-Konventionen umstellen.
