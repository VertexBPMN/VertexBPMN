# Studio UI – Phase 6: Restliche Seiten

Stand: 2026-09-08. Status: abgeschlossen.

## 1. Umfang und Ergebnis

Alle nach Phase 5 verbleibenden Studio-Seiten wurden auf das gemeinsame Designsystem übertragen:

- Operate: `WorkflowTriggers`, `MessagesSignals`.
- Analyze: `EventLog`, `Analytics`, `Performance`, `Health`, `Simulation`, `Debugging`, `Compliance`.
- Manage: `EngineManagement`, `Tenants`, `Credentials`, `Configuration`, `FeatureFlags`, `Migration`, `Connectors`, `Extensions`, `SSO`.
- Sonstige: `Counter` und `Error`.

Counter wurde entsprechend der Plangrenze nicht gelöscht, sondern sichtbar als Demo-/Diagnoseseite eingeordnet. Error bleibt die technische Fallback-Seite und zeigt weiterhin Request-ID und Hinweise zum Development-Modus.

## 2. Umgesetzte Konventionen

- Einheitliche Seitenköpfe mit Arbeitsbereich, Titel, Beschreibung und klarer Primäraktion.
- Formulare und Analyseergebnisse in responsiven `studio-content-card`- beziehungsweise `studio-result-card`-Flächen.
- Tabellen mit gemeinsamen Datenflächen, responsiver Kartenansicht, Leerzuständen und nachgeordneten technischen IDs.
- JSON-Ausgaben sind begrenzt, scrollbar und verwenden die gemeinsame Monospace-Darstellung.
- Tenant-abhängige Seiten zeigen vor einer Auswahl einen expliziten Zustand statt leere Formulare.
- Rollenabhängige Controls und alle bisherigen Aktionen bleiben erhalten.
- Simulation und Debugging behalten vollständige BPMN-XML-Eingaben und alle Engine-Aktionen.
- Migration gliedert Preview/Execute und Live-/Snapshot-Operationen in zwei responsive Arbeitsbereiche.

## 3. Funktionsparität

| Bereich | Erhaltene Use Cases | Status |
|---|---|---|
| Trigger / Messages / Signals | Registrieren, testen, aktivieren/deaktivieren, löschen, korrelieren, broadcasten | grün |
| Event Log / Analytics | Live-Events, Refresh, Modelltraining, Trainingsdatenexport | grün |
| Performance / Health / Compliance | Read-only Betriebs-, Health- und Evidenzdaten | grün |
| Simulation | Run, Szenarien CRUD, Analyse, Vergleich | grün |
| Debugging | Trace, Session, Breakpoint, Step-over, Continue, Visualisierung, Variablen, Replay | grün |
| Tenants / Credentials | CRUD, Tenantwechsel, Secret-Erzeugung/Rotation, OAuth-Verbindung | grün |
| Feature Flags | Laden und rollenabhängiges Umschalten | grün |
| Migration | Preview, Execute, Status, Snapshot, Restore, Rollback | grün |
| Connectors / Extensions | CRUD/Test/Toggle sowie Load/Enable/Disable/Unload | grün |
| Engine / Configuration / SSO | Bestehende read-only Informationen und Capabilities | grün |
| Counter / Error | Interaktion beziehungsweise Fallbackinformationen erhalten | grün |

## 4. Gefundene und behobene Abweichungen

1. Der lokale Stub lieferte für `/api/feature-flags` über den generischen Fallback ein Array statt des vertraglichen JSON-Objekts. Ein expliziter, vertragskonformer Stub wurde ergänzt.
2. Connector-Testergebnisse verwenden den bestehenden Alert-Vertrag auch im erwarteten Fehlerpfad. Dieser Vertrag wurde erhalten; die Meldung wurde nicht in einen generischen Statusbereich umgewandelt.
3. Browser normalisieren Zeilenenden in Textareas auf LF. Der gemeinsame E2E-Eingabehelper übergab unter Windows CRLF ungefiltert, wodurch schnelle Eingaben nie exakt gleich und sequenzielle Eingaben mit doppelten Leerzeilen endeten. Die Eingabegrenze normalisiert nun vor Eingabe und Vergleich auf Browser-LF; XML-Inhalt und Engine-Ausführung bleiben vollständig geprüft.
4. Die erste Screenshotprüfung zeigte Snapshot-Felder außerhalb der Karte „Live migration operations“. Die Razor-Struktur wurde korrigiert und erneut visuell geprüft.

## 5. Verifikation

- Release-Build `VertexBPMN.Studio.UiTests` mit `SkipBpmnIoAssetBuild=true`: **0 Fehler**.
- `StudioUiContractTests`: **50/50 erfolgreich**. Zwanzig neue Fälle prüfen jede Phase-6-Seite bei 390 × 844 px auf Titel, erhaltenen Kerninhalt und fehlenden horizontalen Overflow.
- Lokale Real-E2E-Szenarien gegen WSLC PostgreSQL und RabbitMQ: **13/13 erfolgreich**. Abgedeckt sind Event Log, Simulation, Messages/Signals, Debugging, Migration, Tenants, Credentials, Connectors, Workflow Triggers, Feature Flags, Statusseiten, read-only Administration und Analytics-Export.
- `StudioVisualBaselineTests`: **1/1 erfolgreich**, **62 Screenshots** für sämtliche 31 Seiten auf Desktop und Mobile.
- Layoutmetrik: **0/62** Ansichten mit horizontalem Seitenoverflow.
- Repräsentative Screenshots von Simulation, Debugging, Analytics, Migration und Extensions wurden zusätzlich visuell geprüft; die korrigierte Migration-Mobile-Ansicht hält alle Live-Operationen in einer gemeinsamen Karte.

Bekannt bleiben die bereits vorhandene NU1900-Warnung bei nicht erreichbaren NuGet-Sicherheitsdaten sowie zwei xUnit2013-Analyzerwarnungen außerhalb dieses UI-Umfangs.

## 6. Abnahme

- [x] Kein produktiver Bildschirm verbleibt im alten Stil.
- [x] Sämtliche vorhandenen Aktionen und rollen-/tenantabhängigen Zustände bleiben erhalten.
- [x] Counter inventarisiert und behalten; Error/Fallback modernisiert.
- [x] Contract-, Real-E2E- und visuelle Parität nachgewiesen.
- [x] Desktop und Mobile ohne horizontalen Seitenoverflow nachgewiesen.

Nächster Schritt ist Phase 7: lokale Gesamtabnahme mit Tastatur-/Screenreader-Stichproben, Zoom/Reflow, vollständiger Route-/E2E-Prüfung und kontrollierter Bereinigung verbliebener CSS-Altlasten.
