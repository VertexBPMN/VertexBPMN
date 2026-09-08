# Studio UI: Phase 2 – Designsystem und Shell

Stand: 2026-09-07. Status: abgeschlossen.

## Umgesetzter Umfang

- Zentrales MudBlazor-Theme in `src/VertexBPMN.Studio/Styling/StudioTheme.cs` mit VertexBPMN-Farben, systemlokaler Typografie und einheitlichem Eckenradius.
- Semantische CSS-Tokens in `src/VertexBPMN.Studio/wwwroot/css/studio-tokens.css` für Farben, Abstände, Radien, Schatten und responsive Shell-Geometrie.
- Google-Font-Abhängigkeit entfernt; lokale Segoe-UI-/System-Font-Kette verwendet.
- Responsive App-Bar und Drawer mit sichtbarem Tenant-, Engine- und Verbindungsstatus auf ausreichend breiten Viewports.
- Navigation in die Bereiche Overview, Model, Operate, Analyze und Manage gruppiert. Alle 29 capabilityabhängig sichtbaren Ziele und ihre Routen bleiben erhalten.
- Normale Seiten erhalten einen fluiden, begrenzten Inhaltsbereich; BPMN, DMN, CMMN und Form Builder erhalten einen breiten Workspace-Inhaltsbereich.
- Wiederverwendbare Präsentationskomponenten `StudioPageHeader`, `StudioToolbar` und `StudioStatePanel` als Grundlage für die folgenden Migrationsphasen.
- Sichtbarer Fokus und `prefers-reduced-motion` als globale Basis ergänzt.

## Funktionsschutz

Unverändert blieben API-Verträge, Services, Authentifizierung, Tenant-Auswahl, Engine-Auswahl, Routen, fachliche Seitenhandler sowie sämtliche Modellierer-Interop-Verträge. Kein bpmn.io-, DMN-, CMMN- oder Form-Bundle wurde für diese Phase neu erzeugt oder manuell verändert.

Bootstrap bleibt vorerst geladen. Die Inventur fand als explizite Bootstrap-Control-Verwendung nur die Demo-Seite `/counter`; MudBlazor-Utility-Klassen wie `d-flex` sind davon zu unterscheiden. Die selektive Entfernung erfolgt erst nach Migration oder Entscheidung über diese Demo-Seite, damit kein versteckter Darstellungsbruch entsteht.

## Lokale Verifikation

| Prüfung | Ergebnis |
|---|---|
| Release-Build Studio, serielles MSBuild, bpmn.io-Asset-Build übersprungen | Bestanden, 0 Warnungen, 0 Fehler |
| Release-Build UI-Testprojekt | Bestanden; NU1900 wegen nicht erreichbarer NuGet-Audit-Quelle und zwei bereits vorhandene xUnit2013-Hinweise |
| `StudioUiContractTests` | 24/24 bestanden |
| Neue Navigationsparität | Alle erwarteten 29 Routen vorhanden; fünf Gruppen und Runtime-Kontext erreichbar |
| Neue Shell-Reflow-Fälle | 390×844 und 768×1024 ohne horizontalen Seitenoverflow; Navigation über Drawer erreichbar |
| `StudioVisualBaselineTests` | 1/1 bestanden; sechs Screenshots und Layoutmetriken erzeugt |

Der erste parallele Buildversuch endete in der Projektverweis-Auswertung ohne Compilerdiagnose. Die referenzierten Projekte und der vollständige Studio-Build liefen mit `-m:1` anschließend reproduzierbar erfolgreich. Ein erster Testlauf in der Sandbox wurde durch verweigerte Named-Pipe-Rechte des Microsoft Testing Platform Hosts beendet; derselbe Lauf außerhalb der Sandbox war vollständig grün.

## Visuelle Abnahme und bekannte Grenze

- Dashboard und Tasks belegen bei 390 px exakt die Viewportbreite; die mobile Navigation ist initial geschlossen.
- Desktop zeigt gruppierte Navigation, kompakte Kontextauswahl und die neue visuelle Hierarchie.
- Der BPMN-Modellierer bleibt auf Mobile 660 px breit und erzeugt weiterhin horizontalen Overflow. Dieser bereits in Phase 0 gemessene Editorfehler wird nicht durch Shell-CSS kaschiert, sondern in Phase 3 am tatsächlichen BPMN-Arbeitslayout gelöst.
- Der sehr hohe BPMN-Dokumentfluss bleibt ebenfalls ein explizites Ziel von Phase 3.

## Abnahme

Die Designsystem- und Shell-Grundlage ist umgesetzt und gegen bestehende Verträge abgesichert. Phase 3 kann den BPMN-Modellierer auf diese Grundlage migrieren, ohne Shell, Navigation oder globale Stilquellen erneut grundsätzlich umzubauen.
