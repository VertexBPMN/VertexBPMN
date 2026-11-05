# ADR-0002 — Fachportal und Portal-BFF (Low-Code-Anwendungsplattform)

- **Status:** Vorgeschlagen (LC01; Umsetzung erst mit LC06)
- **Datum:** 2026-10-13
- **Entscheider:** Yovanny Rodríguez (Yova) / Product & Operations
- **Kontext:** [Low-Code-Anwendungsplattform-Plan](2026-10-13_Low-Code-Anwendungsplattform_Implementierungsplan.md), §3.1, LC06. Fachnutzer sollen veröffentlichte Anwendungen ohne Studio-Zugang nutzen.

## Entscheidung

- Ein **separater optionaler Fachportal-Dienst** (`VertexBPMN.Portal` als ASP.NET-Core-Host) plus `VertexBPMN.Portal.Client` (Blazor-WebAssembly) wird in Solution und AppHost integriert. Er erzeugt **keine zweite Engine**.
- Der **Portal-BFF** ist der serverseitige Ansprechpartner des Browsers: Anmeldung, Cookie-/Tokenhaltung, Logout, Refresh und autorisierte API-Aufrufe laufen serverseitig. **Keine Tokens im Browserzustand** (LocalStorage/SessionStorage/UI-Payload).
- Wiederverwendung vorhandener OIDC-Bausteine erst **nach LC01-Inventur**; nur minimal nötiger gemeinsamer Authentifizierungscode wird in eine kleine gemeinsame Bibliothek herausgelöst. Kein vorschnelles Refactoring.
- Cookie-Mutationen besitzen CSRF-Schutz; zwei Portal-Replikate teilen keine In-Process-Sitzung (separater Abnahme-Vertrag).

## Grenzen

- Veröffentlichte Seiten rendern nur **bekannte Komponenten** aus Metadaten; kein frei ausführbares JavaScript, kein `eval`, keine rohen HTML-Vorlagen/automatisch installierte Komponenten.
- UI-Verstecken ersetzt keine Autorisierung; Daten-/Aktionsauflösung erfolgt serverseitig gegen veröffentlichtes Release und Nutzerrechte.
- Das Portal ist optional; bestehende Studio-Navigation und Betriebsprofile bleiben erhalten.

## Konsequenzen

- LC06 ist entkoppelt implementierbar; Portal-BFF-Endpunkte erhalten eine eigene `.http`-Datei (`Portal/Http/portal-bff.http`).
- Browser-/Offline-Abläufe bleiben Browserabnahmen; die `.http`-Datei umgeht den Anmeldevertrag nicht.
