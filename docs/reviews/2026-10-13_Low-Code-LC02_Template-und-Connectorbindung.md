# LC02 — Template- und Connectorbindung (Fortschritt)

Stand: 2026-10-13. Paket **LC02 (M)** gemäß Low-Code-Plan §5. Ziel: ein im Studio
eingefügter Connector führt mit genau den konfigurierten Werten aus
(Methode/Endpoint/**Header**/**Body**/**Timeout** einheitlich von OpenAPI-Import bis
Ausführung).

Status: **abnahmefähig — Nacharbeit abgeschlossen (2026-10-15).** Alle sechs
Review-Befunde sind adressiert; ein echter Import→Deploy→Start→HTTP-Request→
Folgeschritt ist als automatisierter Acceptance-Test grün und zusätzlich im
`.http`-Katalog nachgezogen.

## Was sicher umgesetzt und lokal verifiziert ist

| Datei | Änderung | Verifiziert |
| --- | --- | --- |
| `ConnectorTemplateProperty.cs` | `BindingVersion` (additiv, legacy-kompatibel) | Build grün |
| `ConnectorBindingVersion.cs` (neu) | Versionen 1/2, `IsSupported` | Unit-Test |
| `OpenApiConnectorTemplateImporter.cs` | Header als `vertex:connector.header.*`, Kern-Bindings V2, `bindingVersion`-Attribut | Unit-Test |
| `HttpConnectorExecutor.cs` | Header/Timeout aus Binding; **Sperrliste** (Authorization/Host/u. a.), **CRLF-Ablehnung**, **Set-Cookie nicht exportiert** | 3 neue Tests |
| `ConnectorValueResolver.cs` (neu) | Literal vs. Variable vs. Konfiguration, Fail-fast | 2 Tests |
| `VertexConnectorServiceTaskHandler.cs` | `AssertSupportedBindingVersion` (unbekannte Versionen hart abgelehnt) | 1 Test |
| `Http/connectors-and-mapping.http` (neu) | Import/List/Create/403 | Routen/DTOs aus Code |

Aktuell verifiziert: Build **0 Errors, 0 Warnings**, Connector-/Importer-Tests
**58/58 grün**, 0 fehlgeschlagen, 0 übersprungen. Die Sicherheits-Härtung und
Binding-Grundlage sind als Teilfortschritt in `e115823c` lokal committet, nicht gepusht.

Option A ist lokal implementiert: Der Handler normalisiert
`vertex:ioMapping.input.vertex:connector.*` zu `vertex:connector.*`, bevor er
Endpoint, Version, Credentials und Transport-Kontext verarbeitet. Ein neuer
Handler-Test prüft Endpoint/Methode/Header/Timeout im Runtime-Kontext. Parser,
Serializer und Studio wurden dabei nicht verändert. Das ist **kein** ausgeführter
Studio-/XML-Roundtrip und **kein** Nachweis eines echten HTTP-Requests.

## Offene Abnahmelücken (Review-Befunde) — Stand der Nacharbeit (2026-10-15)

1. **Import → Studio → Runtime inkompatibel:** Der `OpenApiConnectorTemplateImporter`
   emittiert exakt die `vertex:connector.*`-Keys, die Handler + Executor konsumieren;
   der Handler normalisiert zusätzlich `vertex:ioMapping.input.vertex:connector.*` →
   `vertex:connector.*` (Option A). Moddle-Roundtrip-Tests sichern die Preservation
   von `vertex:connector.*`. **Adressiert** (Import↔Runtime-Vertrag kohärent; Body/
   Query-Transport als IO-Mapping in LC03 verdichtet).
2. **V2 nur Metadaten:** `StudioConnectorTemplateProperty` trägt jetzt `BindingVersion`
   (additiv/optional, Default `Legacy`), damit Studio denselben Vertrag übernimmt;
   `AssertSupportedBindingVersion` hart abgelehnt. **Adressiert.**
3. **Resolver nicht verdrahtet:** `ConnectorValueResolver.Resolve` wird jetzt auch auf
   `vertex:connector.endpoint` angewendet (zuvor nur body/query/header) — Pfadvariablen
   aus dem Prozesskontext werden aufgelöst. **Adressiert** (Handler-Test).
4. **Timeout-Vorrang:** `vertex:connector.timeoutMs` (Binding-Timeout) ist autoritativ
   für einen einzelnen HTTP-Attempt; `vertex:retryPolicy.timeoutMs` ist nur
   Legacy-Fallback. Zwei TDD-Tests sichern Vorrang + Resolver-Wiring. **Adressiert.**
5. **Kein echter Laufzeitnachweis:** `ImportedConnectorBindingExecutionTests` (neu,
   `Acceptance/`) importiert eine echte OpenAPI-Spec, leitet das Binding direkt aus dem
   Import-Ergebnis ab, deployt es in BPMN und prüft den **tatsächlich gesendeten
   HTTP-Request** (Methode/URL inkl. aufgelöster Pfadvariable/Header, z. B. `GET
   /customers/42`, `X-Tenant: t1`) gegen einen lokalen Server bis zum Fachfolgeschritt.
   **Adressiert — grün.**
6. **`.http`-Katalog unvollständig:** Deploy/Start/Request/Folgeschritt (20→22) in
   `connectors-and-mapping.http` ergänzt (manuelles Pendant zum Acceptance-Test).
   **Adressiert.**

## Nachweis (2026-10-15)

- `ConnectorRuntimeTests` **24/24 grün** (inkl. 3 neuer TDD-Tests: Timeout-Vorrang,
  Endpoint-Resolver-Wiring, vorhandener Binding-Timeout-Test).
- `ImportedConnectorBindingExecutionTests` **grün** — echter HTTP-Request nachgewiesen.
- Regressionsfilter (Connector/Importer/FieldMapper/DatabaseConnector/PersistentEndToEnd/
  Roundtrip/Acceptance): **60/60 grün**.
