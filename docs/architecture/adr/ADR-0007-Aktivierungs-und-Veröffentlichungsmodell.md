# ADR-0007 — Aktivierungs- und Veröffentlichungsmodell (Editions-Direktorium)

- **Status:** Vorgeschlagen (LC01; Umsetzung ab LC10)
- **Datum:** 2026-10-13
- **Entscheider:** Yovanny Rodríguez (Yova) / Product & Operations
- **Kontext:** [Low-Code-Anwendungsplattform-Plan](2026-10-13_Low-Code-Anwendungsplattform_Implementierungsplan.md), §5 LC01. Veröffentlichung und Aktivierung von Anwendungsreleases folgen einem Aktivierungs-/Veröffentlichungsmodell mit Editions-Direktorium und App-Downgrade-logik.

## Entscheidung

- **Isolierte Revisionen, inkrementelles App-Downgrade:** veröffentlichte Anwendungs-/App-Release-Editions sind isolierte Revisionen; App-Downgrade ist inkrementell definierbar (kein globaler Zurücksetzungs-/Abbruch-Knopf). Semantisch unerlaubte Downgrades (nicht passende Editions-/Schemaebene) werden **verboten**, nicht laut vermerkt.
- **Aktivierungsdeltas:** Der Unterschied zwischen Editions-/Aktivierungsstand liefert ein Aktivierungsdelta; Aktivierung ist editions-/veröffentlichungsbasiert (kein reines Versionsfeld).
- `AppReleaseDefinitionRecord`, `AppRelease`, **Instanz-basierte Permission-Hooks** nach OIDC-Bausteine-Inventur und verwandte Release-Konzepte sammeln veröffentlichungs-/aktivierungsbezogene Einheiten in einem **gemeinsamen, versionierten „Editions- und Veröffentlichungsmodell“**; nicht aufgeteilt auf UN-und sonstige enge Modelle.

## Konsequenzen

- Instanzbasierte Rechte (data-scoped) und Mandantenisolation hängen an Editions-/Release-Vertrag, nicht an Update/Live-Overwrite.
- Event-/Endpunkt-Aktivität (LC09) und Admin-Extraktion (LC10) prüfen Editions-/aktivierungsbezogene Zustände über dieses Modell.
- Die LC01-Doku enthält Rollen-/Zustandsmatrix (Statusmodell), die Aktivierung/Zustand gegen Rechte prüft (RBAC-Aspekt).
