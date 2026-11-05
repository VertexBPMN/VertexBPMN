# ADR-0003 — Optionale Node-RED-Laufzeit für technische Flows

- **Status:** Vorgeschlagen (LC01; Umsetzung erst mit LC08)
- **Datum:** 2026-10-13
- **Entscheider:** Yovanny Rodríguez (Yova) / Product & Operations
- **Kontext:** [Low-Code-Anwendungsplattform-Plan](2026-10-13_Low-Code-Anwendungsplattform_Implementierungsplan.md), §3.1/§8. Node-RED ist die empfohlene technische Laufzeit für visuell programmierte Flows.

## Entscheidung

- **Node-RED als optionale externe Laufzeit** empfohlen; ein Austausch erfordert einen begründeten ADR mit Anpassung von LC08 bis LC10.
- LC08 beginnt mit einem **begrenzten Architekturversuch** mit gepinnten Node.js-/Node-RED-Versionen (eigener Prozess, Vertex-Claim/Heartbeat/Completion, Authentifizierung, Kill/Restart); kein Produktionsspike als fertige Integration markieren.
- Vertex-External-Task-Nodes unter `integrations/node-red/vertex-nodes/`; Node-/npm-Paketnamen sind neu festzulegen. **ProcessCube-Nodes sind keine unverändert nutzbaren Vertex-Adapter.**

## Grenzen (Plan §3.2)

- **Node-RED ist keine Sandbox:** pro Mandant bzw. nachgewiesen gleichberechtigtem Vertrauensbereich eine getrennte Laufzeit mit eigener Workeridentität, begrenztem Netz-/Dateizugriff und Ressourcenlimits. Keine gemeinsam privilegierte Laufzeit für fremde Mandanten; Editor- und Laufzeitrechte getrennt.
- Node-RED-Context und Browserzustand ersetzen **keine** Engine-Instanzen, Inbox, Outbox oder Tasks. Kritische Arbeit läuft über External Tasks und dauerhafte Fortsetzungen.
- Der konfigurierte External-Task-Vertragsresolver wird für Objekt-/Array-Inputs begrenzt erweitert; skalare Legacy-Verträge bleiben unverändert. Kein JSON-in-String-Workaround.
- Credentialauflösung ist ein eng berechtigter interner Vorgang, kein generischer Secret-Node für beliebige Flows.

## Konsequenzen

- **Stufe B und bestehende Installationen funktionieren ohne Node.js, Node-RED, Docker oder Cloud-Zugang.** Kein Node-RED-Autostart/npm-Download im normalen .NET-Testlauf.
- Native lokale Prozessführung, Existing-Service-Modus und optional WSLC-/Containerprofil.

## Vorbehalt

- LC08-Architekturversuch kann die Empfehlung ändern; bei nachgewiesener Unverträglichkeit ADR und Plan vor Ausbau ändern.
