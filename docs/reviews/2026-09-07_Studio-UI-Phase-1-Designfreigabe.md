# Studio UI: Phase-1-Designfreigabe

Stand: 2026-09-07. Status: abgeschlossen; Designrichtung durch die Aufforderung zur Fortsetzung freigegeben.

## Entwurfsrichtung

Der interaktive Entwurf umfasst dieselbe Designsprache für:

- Shell und Dashboard;
- BPMN-Arbeitsbereich;
- Aufgabenliste;
- Desktop- und Mobile-Darstellung.

Die Richtung ist ein ruhiges, professionelles Arbeitswerkzeug mit eigener VertexBPMN-Identität. MudBlazor bleibt die technische Komponentenbasis. Der Entwurf ist nicht mit der produktiven Blazor-Anwendung verbunden und verändert keine Funktion.

## Vorgeschlagene Entscheidungen

| Bereich | Vorschlag | Funktionale Grenze |
|---|---|---|
| Navigation | Gruppiert in Overview, Model, Operate, Analyze und Manage | Alle existierenden Routen bleiben erreichbar; CMMN bleibt capabilityabhängig. |
| Kopfzeile | Produkt links; Tenant, Engine, Verbindung und Benutzer rechts | Tenant und Engine bleiben getrennte Kontexte; Status ist nicht nur farbcodiert. |
| Dashboard | Kompakte Kennzahlen, aktuelle Aktivität und Aufgaben | Nur bestehende Daten; keine erfundenen API-Abfragen oder KPI. |
| BPMN | Canvas als Hauptfläche, Eigenschaften rechts, Diagnose-/XML-/Versionswerkzeuge unten als Tabs | Deploy bleibt Deploy; Auswahl, Zoom, XML, Undo/Redo und Modeler-Instanz dürfen beim Panelwechsel nicht verloren gehen. |
| Tasks | Aufgabe und Prozess vor technischen IDs; Claim als beschriftete Hauptaktion | Bestehende Claim-/Complete-Regeln, Suche, Sortierung und Detailansicht bleiben unverändert. |
| Mobile | Kompakte AppBar, vier primäre Navigationsziele, „More“ für vollständige Navigation | Kein Menüpunkt verschwindet; Modeler-Eigenschaften benötigen eine zugängliche Bottom-Sheet-/Panel-Lösung. |
| Stil | Segoe UI/system-ui, neutrale Flächen, Blau als primärer Akzent, sparsame Statusfarben | Finale Tokens müssen Kontrastprüfung bestehen. |
| Dichte | „Comfortable“ als Standard, „Compact“ als prüfbare Alternative | Dichte ändert keine Daten oder Aktionen. |

## Im Prototyp nachgewiesen

- Dashboard, BPMN und Tasks lassen sich lokal umschalten.
- Desktop-/Mobile-Vorschau lässt sich lokal umschalten.
- Der entworfene Mobile-Rahmen hat bei 388 px Breite keinen eigenen horizontalen Overflow (`clientWidth=388`, `scrollWidth=388`).
- Alle im Entwurf sichtbaren primären Controls besitzen Text oder zugängliche Namen.
- BPMN zeigt weiterhin Add node, Import, Export, Load template, Validate und Deploy; sekundäre Funktionen sind als Modellwerkzeuge sichtbar vorgesehen.
- Tasks zeigt weiterhin Suche, Refresh, Claim und Details; technische IDs bleiben verfügbar, aber sekundär.
- Designparameter Akzent, Eckenradius und Dichte sind im Entwurf variierbar.

## Noch vor Phase 2 zu entscheiden

1. Designsprache freigeben oder konkrete Änderungswünsche benennen.
2. Navigation auf Englisch belassen oder eine gesonderte, vollständige Lokalisierungsphase planen. Keine gemischte Teilübersetzung.
3. „Comfortable“ oder „Compact“ als Standarddichte festlegen.
4. Mobile BPMN-Eigenschaften als Bottom Sheet oder als eigene fokussierte Detailansicht gestalten.
5. Optionalen Dark Mode jetzt mitbauen oder erst nach dem vollständig abgenommenen Light Theme ergänzen.

## Abnahmekriterium Phase 1

Phase 1 wird erst als abgeschlossen markiert, wenn die visuelle Richtung und die fünf offenen Entscheidungen bestätigt oder angepasst wurden. Erst danach beginnt die Umsetzung des Designsystems und der Shell in Phase 2.
