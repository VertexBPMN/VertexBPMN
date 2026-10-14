# ADR-0004 — Vertrauensbereiche, Mandantenumgebung und Node-RED-Sandbox

- **Status:** Vorgeschlagen (LC01; Umsetzung anwendbar ab LC06/L08)
- **Datum:** 2026-10-13
- **Entscheider:** Yovanny Rodríguez (Yova) / Product & Operations
- **Kontext:** [Low-Code-Anwendungsplattform-Plan](2026-10-13_Low-Code-Anwendungsplattform_Implementierungsplan.md), §3.2. Mandantentrennung und Sandboxing der technischen Laufzeit sind Kern-Sicherheitsziele.

## Entscheidung

- **Mehrere Vertrauensbereiche** sind die Referenzarchitektur: je **gleichberechtigter Vertrauensgruppe ein eigener Bereich** (getrennte Credential- und Secret-Auflösung, eigene Engine-/Kafka-/Task-Lebenszyklen mit eigenem Containerprofil, optional eigene Laufzeit). **Vereinheitlichung ist Ausnahme**, nie Ergebnisse physischer Replikation; herkunftsgetreue Zuordnung bleibt bestehen, Vorgänge zeigen die echte ID.
- **Isolierte Prozessinstanz je Mandant** (`ProcessSpotlight` + CRUD auf einen Persistenz-Kontext mit einheitlicher Mandanten-ID) als Referenz für Sandboxing. Ein eigener Mandant SAMT Node-RED bekommt **keine** gemeinsame installierte Teilmenge.
- **Konsistenter Kernel-Sandbox-Vertrag:** Flow-Schritt, `ConditionalDecorate`, `ProcessSpotlight`, direkte `vTuple`-Zugriffe (mit klarem Fehler) im Op-Box-Kompatibilitätsmodus; Kennzeichnung via `hasSemantics`/`AccessMode.Direct` als **Plan-voraus-Sandboxlandkarte** für Tests.
- Mandantenumgebung getrennt ausrollbar; Migrationen mandantenlokal/konsistent.

## Node-RED-Sandbox-Vertrag

- **Node-RED-Kern(-Community)-Nodes sind keine generischen Webhooks:** Von HTTP/WebHook-Nodes ausgehende Verbindungen werden gegen eine zentrale Erlaubnisliste reguliert; **In-Webservices** nur unter Op-Kern oder mit eigener Richtlinie. Community-Einführung kein Free-Pass ohne obige Sandbox-Kennzeichnung.
- Editor-Publishing, Media-Type-Handling, Datei-/Verzeichniszugriff, Environment (Container), Netz und einschlägige `viaAssignment`-Datenfelder stehen in der Sandbox-Regelmatrix (unter `integrations/node-red/`).

## Konsequenzen

- Test-/Abnahme-Catalog und Depot (LC09 §9A „Test-Catalog / Depot“) prüfen Sandbox-Kennzeichnung, Vertrauensbereiche und Reader-Größe.
- Neue Admin-Endpunkte (LC10) folgen dieser ADR und dokumentieren im ADR/der Regelmappe.
