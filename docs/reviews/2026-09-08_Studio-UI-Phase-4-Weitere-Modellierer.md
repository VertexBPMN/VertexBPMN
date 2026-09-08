# Studio UI: Phase 4 – Weitere Modellierer

Stand: 2026-09-08. Status: abgeschlossen, einschließlich realer lokaler WSLC-Abnahme.

## Umgesetzter Umfang

- DMN Modeler, CMMN Modeler und Form Builder verwenden den gemeinsamen Seitenkopf, die responsive Aktionsleiste und die primäre Modeler-Karte des Studio-Designsystems.
- Alle vorhandenen Aktionen, Eingabefelder, Handler, API-Verträge und `data-testid`-Selektoren bleiben erhalten.
- Editor-spezifische Funktionen wurden nicht vereinheitlicht oder durch BPMN-Funktionen ersetzt:
  - DMN behält Decision-Key/Name, Regelbearbeitung, Evaluation sowie Definition-/Instanzübersicht.
  - CMMN behält Registrierung, Ausführung, Human-Task-Erzeugung, User Events, Case-File-Updates, Ad-hoc-Erzeugung und Historie.
  - Form Builder behält Registry-Speicherung, Import/Export, gespeicherte Formulare und die programmatische Textfeld-Erzeugung.
- Die editierbaren Modeler bleiben bei Werkzeugwechseln gemountet. Read-only Viewer werden erst beim Öffnen des jeweiligen Viewer-Tabs erzeugt; dadurch konkurrieren sie nicht dauerhaft um Höhe und erhalten stets sichtbare Containerabmessungen.
- Lange Runtime-, History- und JSON-Ausgaben sind innerhalb ihrer Werkzeugbereiche begrenzt bzw. scrollbar.
- Aktionsleisten, Eingabefelder und Werkzeugbereiche brechen auf schmalen Viewports kontrolliert um.

## Lokale Verifikation

| Prüfung | Ergebnis |
|---|---|
| Release-Build des UI-Testprojekts mit unveränderten bpmn.io-Artefakten | Bestanden; nur bekannte NU1900- und zwei xUnit2013-Warnungen |
| Gesamte `StudioUiContractTests` nach Phase 4 | 25/25 bestanden |
| Reales DMN Import/Regel/Deploy/Reload/Evaluate/Export-Szenario | Bestanden |
| Reales Form Import/Save/Reload/Edit/Update/Export-Szenario | Bestanden |
| Reales CMMN Import/Register/Execute/Event/Case-File/History/Export-Szenario | Bestanden |
| Gemeinsamer realer Phase-4-Lauf | 3/3 bestanden in 164,453 Sekunden; isolierte Datenbanken anschließend bereinigt |
| Lokale visuelle Desktop-/Mobile-Abnahme | 1/1 Test bestanden; zwölf Szenario-/Viewport-Kombinationen ohne horizontalen Overflow |

## Gemessene Layouts

| Editor | Desktop 1440×900 | Mobile 390×844 |
|---|---:|---:|
| DMN Modeler | Dokument 1440×1276 | Dokument 390×1469 |
| CMMN Modeler | Dokument 1440×1332 | Dokument 390×1633 |
| Form Builder | Dokument 1440×1324 | Dokument 390×1431 |

Die CMMN-Höhe enthält bewusst die umfangreichen Laufzeit- und Historieninformationen des realen Abnahmeszenarios. Die Baseline ohne Laufzeitdaten bleibt kompakter; in beiden Fällen wird kein horizontaler Seitenoverflow erzeugt.

## Ergebnis

Die drei weiteren Modellierer erfüllen die Phase-4-Paritätsanforderung und sind in das gemeinsame Studio-Arbeitslayout übertragen. Die nächste Planphase ist Phase 5 – operative Kernseiten.
