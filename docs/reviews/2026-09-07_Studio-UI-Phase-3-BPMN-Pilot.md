# Studio UI: Phase 3 – BPMN-Pilot

Stand: 2026-09-08. Status: abgeschlossen, einschließlich realer lokaler WSLC-Infrastrukturabnahme.

## Umgesetzter Umfang

- BPMN-Seitenkopf und Aktionsleiste auf die neuen Phase-2-Bausteine übertragen.
- Bestehende Aktionen Deploy, Add node, Validate, BPMN-/n8n-Import, Export und Template-Laden unverändert angebunden und auf schmalen Viewports umbrechend angeordnet.
- Modeler als primäre Arbeitsfläche mit begrenzter viewportbezogener Höhe statt unkontrolliert wachsender Seite gestaltet.
- Validation, XML, Connectors, Test runs, Versions und Viewer als semantische, horizontal scrollbar erreichbare Werkzeugbereiche gebündelt.
- Der editierbare Modeler bleibt bei Werkzeugwechseln permanent gemountet. Auswahl, Modellinhalt und Undo-/Redo-Stack werden dadurch nicht durch eine neue Modeler-Instanz ersetzt.
- Mobile Properties als standardmäßig geschlossenes Bottom Sheet umgesetzt. Öffnen und Schließen vermisst den Modeler-Canvas neu.
- Viewer-Interop gegen unsichtbare Container gehärtet: XML-Import bleibt möglich, `fit-viewport` wird nur mit endlichen sichtbaren Abmessungen ausgeführt, und beim Öffnen des Viewer-Werkzeugs erfolgt eine explizite Neuvermessung.
- Validierung und blockierte Deployments aktivieren den Validierungsbereich, damit Fehler nicht in einem anderen Werkzeug verborgen bleiben.

## Gefundene und behobene Root Cause

Ein zunächst dauerhaft versteckt gemounteter `BpmnViewerSurface` erhielt nach einer Diagrammänderung neues XML. Der Viewer führte anschließend `zoom('fit-viewport')` auf einem `display:none`-Container aus. bpmn.io erzeugte dadurch eine nicht-endliche SVG-Skalierung; die JS-Exception beendete den Blazor-Circuit und weitere UI-Aktionen reagierten nicht mehr.

Die Korrektur liegt nicht im Test: `bpmn-viewer.js` prüft reale Containerabmessungen vor dem Fit und bietet eine Resize-Operation. `BpmnViewerSurface` und die BPMN-Seite rufen diese beim sichtbaren Öffnen auf. Der zuvor fehlschlagende Quick-Insert-→-XML-Use-Case ist danach grün.

## Gemessene Darstellung

| Szenario | Phase-0-Baseline | Aktueller Stand |
|---|---:|---:|
| BPMN Desktop, Dokumenthöhe bei 1440×900 | 5.071 px | 1.268 px |
| BPMN Mobile, Dokumentbreite bei 390×844 | 660 px | 390 px |
| BPMN Mobile, Dokumenthöhe bei 390×844 | 3.520 px | 1.360 px |

Damit ist der unkontrollierte horizontale Seitenoverflow beseitigt. Die Werkzeugtabs scrollen auf Mobile gezielt horizontal, ohne die Dokumentbreite zu vergrößern.

## Lokale Verifikation

| Prüfung | Ergebnis |
|---|---|
| Release-Build UI-Testprojekt, bpmn.io-Asset-Neubau übersprungen | Bestanden; nur bekannte NU1900- und zwei xUnit2013-Warnungen |
| Gezielter Quick-Insert-→-XML-Fall nach Viewer-Korrektur | Bestanden |
| Mobiles Properties-Sheet inklusive Reflow | Bestanden |
| Gesamte `StudioUiContractTests` | 25/25 bestanden |
| `StudioVisualBaselineTests` | 1/1 bestanden; sechs Screenshots/Metriken aktualisiert |
| Reale API-/Studio-Readiness mit WSLC PostgreSQL und RabbitMQ | 1/1 bestanden; fünf isolierte Datenbanken erstellt und nach dem Lauf verifiziert gelöscht |
| Reales BPMN Import/Edit/Validate/Deploy/Reload/Export/Reimport/Versions-Szenario | 1/1 bestanden |
| Reale lokale Simulation und Engine-Testausführung | 1/1 bestanden |
| Reales n8n Import/Validate/Deploy/Reload/Export-Szenario | 1/1 bestanden |
| Reales bpmn.io Routing und Undo/Redo | 1/1 bestanden |
| Reale Workflow-Trigger-/Webhook-Lebensdauer | 1/1 bestanden; Registrierung, Aufruf, Deaktivierung und Löschung geprüft |

## Abschluss der Infrastrukturabnahme

Die gezielten `LocalStudioInfrastructureTests` liefen lokal gegen die bereits vorhandenen WSLC-Container `vertexbpmn-postgres` und `vertexbpmn-rabbitmq`. API und Studio wurden vom Testhost auf freien Loopback-Ports gestartet; persistente Testdaten lagen ausschließlich in laufbezogenen Datenbanken und wurden beim Teardown entfernt.

Während der Abnahme wurden keine fachlichen Assertions entfernt. Die Real-E2E-Abläufe öffnen die neuen Werkzeugtabs ausdrücklich. Zusätzlich selektiert Quick Insert das neu erzeugte Element jetzt unmittelbar, sodass dessen Properties ohne erneute Diagrammsuche bearbeitet werden können. Der nachfolgende XML-Wechsel, die Persistenzprüfung und der Export bestätigen, dass die Änderung tatsächlich im BPMN-Artefakt enthalten ist.
