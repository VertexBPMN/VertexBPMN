# Studio UI – Phase 7: Lokale Gesamtabnahme

Stand: 2026-09-08. Status: abgeschlossen.

## Umfang

Phase 7 nimmt das Redesign aus den Phasen 0 bis 6 lokal ab. Geprüft wurden alle 31 Studio-Routen, die vier geplanten Referenzgrößen, reale Tastaturbedienung und Fokusführung, semantische Screenreader-Grundlagen, Zoom/Reflow, BPMN-Arbeitsfläche, Textkontrast, Ladeverhalten und ausgewählte reale E2E-Wege gegen API, PostgreSQL und RabbitMQ über WSLC. Es wurden keine GitHub-Workflow-Prüfungen ergänzt.

## Behobene Befunde

| Befund | Ursache | Korrektur | Nachweis |
|---|---|---|---|
| Fokus war an MudBlazor-Aktionen nicht zuverlässig sichtbar | Die Bibliothekskaskade überstimmte die schwache globale Fokusregel. | Verbindlicher `:focus-visible`-Ring nur für interaktive Elemente; Blazors programmatisch fokussierte H1 bleibt ohne irreführenden Rahmen. | Echte Tab-/Enter-Navigation für Menü und BPMN-Link; Fokuszustand im Browser berechnet. |
| CMMN und Form Builder benötigten lokal etwa 25–28 Sekunden | Serverkomponenten luden eigene Dateien über den fest auf `http://localhost/` gesetzten `Default`-HttpClient und warteten außerhalb Port 80 auf den Timeout. | Vorlagen werden direkt aus `IWebHostEnvironment.WebRootPath` gelesen; unbenutzter `Default`-Client entfernt. | Beide Editoren werden im Abnahmetest in unter fünf Sekunden bereit; finale Matrix maximal 841 ms. |
| Tenantgebundene mobile Seiten waren nicht bedienbar | Bei 390 px wurde der zwingend benötigte Tenant-Selector ausgeblendet, der Engine-Selector blieb sichtbar. | Mobile Priorität umgekehrt: Tenant sichtbar, Engine und Status platzsparend ausgeblendet. | Realer WSLC-Mobiltest wählt den Tenant und rendert vier Schlüsselrouten fehlerfrei. |
| Sekundärtext verfehlte AA knapp | `#64748b` ergab auf `#f6f8fb` nur 4,47:1. | Zentraler Muted-Token auf `#627187` angepasst. | Browserbasierte Kontrastprüfung: mindestens 4,5:1; rechnerischer Tokenwert 4,66:1. |
| Lokale Komplettausführung meldete E2E-Fehler ohne Opt-in | Zehn Infrastrukturtests griffen vor ihrer fehlenden `SkipUnless`-Voraussetzung auf den nicht initialisierten Host zu; ein Shell-Test klickte vor InteractiveServer-Hydrierung. | Einheitliche lokale E2E-Voraussetzung und explizites `data-interactive-ready` für Interaktionstests. | Gesamte UI-Testassembly: 156 entdeckt, 57 bestanden, 99 erwartungsgemäß übersprungen, 0 fehlgeschlagen. |
| Template-CSS blieb im globalen Stylesheet | Nicht verwendete `.btn-primary`-, `.btn-link`-, `.btn...:focus`- und `.content`-Regeln stammten aus dem Starttemplate. | Belegte Altregeln entfernt; Blazor-Validierungs- und Error-Boundary-Regeln bewusst erhalten. | Quelltextsuche vor Entfernung und vollständige UI-Vertragssuite danach. |

## Messergebnisse

### Visuelle Matrix

- 31 Routen × 4 Viewports = **124 Screenshots**.
- Viewports: 1440×900, 1280×800, 1024×768 und 390×844.
- **0** Fälle mit unkontrolliertem horizontalem Seitenoverflow.
- Finale mittlere Zeit bis zum definierten Bereitschaftszustand: **572,9 ms**.
- Finales Maximum: **840,9 ms**.
- Mittlere Navigation: **533,8 ms**, Maximum **812,2 ms**.
- Metriken und lokale Bilder: `tests/VertexBPMN.Studio.UiTests/TestResults/ui-modernization-baseline/`.

### Accessibility, Tastatur und Reflow

`StudioUiAcceptanceTests` deckt acht lokale Abnahmen ab:

1. genau ein Main-Landmark und eine H1 auf der geprüften Route sowie gesetzte Dokumentsprache;
2. echte Tab-Navigation, sichtbarer Fokus und Aktivierung von Shell und Navigation mit Enter;
3. Mindestzielgröße von 24×24 CSS-Pixeln am mobilen Hauptmenü;
4. Fokus-Rückkehr zum Auslöser nach Schließen eines Dialogs;
5. Reflow ohne Seitenoverflow bei 640×400 (1280×800 bei 200 %) und 320×256 (400 %-äquivalent) auf Dashboard, Tasks und BPMN Modeler;
6. mindestens 500 px BPMN-Canvas-Höhe bei 1280×800;
7. CMMN/Form-Builder-Bereitschaft ohne Netzwerk-Timeout und unter fünf Sekunden;
8. WCAG-AA-Kontrast von mindestens 4,5:1 für normalen Sekundärtext.

Diese Prüfungen belegen die Shell und repräsentative HTML-/MudBlazor-Abläufe. Sie sind ausdrücklich keine pauschale Screenreader-Zertifizierung der eingebetteten bpmn.io-, dmn-js-, cmmn-js- oder form-js-Canvas-Editoren.

## Testnachweise

| Lauf | Ergebnis |
|---|---|
| Release-Build `VertexBPMN.Studio.UiTests` | erfolgreich; nur bekannte NU1900- und zwei xUnit2013-Warnungen |
| `StudioUiAcceptanceTests` | **8/8 bestanden** |
| vollständige UI-Testassembly ohne Infrastruktur-Opt-in | **156 entdeckt, 57 bestanden, 99 übersprungen, 0 fehlgeschlagen** |
| finale Screenshot-/Metrikmatrix | **1/1 bestanden, 124 Screenshots, 0 Overflow** |
| reale WSLC-Direktnavigation und Reload aller Routen | **30/30 bestanden** |
| realer WSLC-Mobiltest der Schlüsselrouten | **1/1 bestanden** |

## Getrennte Beobachtungen außerhalb des UI-Redesigns

- Bei mehreren unmittelbar aufeinanderfolgenden isolierten E2E-Datenbankinitialisierungen reagierte PostgreSQL zeitweise nicht innerhalb des 10-Sekunden-Verbindungszeitraums. Nach kontrolliertem Neustart der zwei lokalen WSLC-Container lief die Abnahme stabil. Volumes und Daten wurden nicht gelöscht.
- Das PostgreSQL-Log zeigt unabhängig davon einen vorhandenen OAuth2-Persistenzfehler: `OAuth2FlowStates.ExpiresAt` ist in der Migration als Text angelegt, wird aber mit `now()` verglichen (`text <= timestamp with time zone`). Dieser Backend-/Migrationsfehler gehört nicht zum UI-Redesign und wurde hier nicht verdeckt oder fachfremd geändert.
- Die NuGet-Auditquelle war lokal zeitweise nicht erreichbar (`NU1900`). Die zwei bestehenden xUnit2013-Hinweise liegen in MultiInstance-/SubProcesses-Tests und sind nicht durch Phase 7 entstanden.

## Abschlussurteil

Die messbaren UI-Abnahmekriterien des Modernisierungsplans sind erfüllt. Alle produktiven Studio-Routen besitzen die neue Designsprache, die geprüften Bedienwege bleiben erhalten, die Shell ist in den vorgesehenen Größen reflow-fähig und die reale lokale API-/WSLC-Anbindung wurde für Route/Reload und mobile Schlüsselrouten bestätigt. Die oben getrennt aufgeführten Editor-Accessibility-Grenzen und Backendbefunde dürfen nicht als durch das Redesign gelöst interpretiert werden.
