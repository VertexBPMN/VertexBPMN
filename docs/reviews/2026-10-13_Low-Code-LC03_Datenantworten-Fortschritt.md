# LC03 Datenantworten Abfragen und visueller Mapper — Fortschrittsbericht

Stand: 2026-10-14 · Branch `codex/low-code-lc00` · Paket L (abhängig von LC02)

## Status: offen — Teilfortschritt, noch nicht abnahmefähig

LC03 ist ein großes Paket mit mehreren, teils voneinander unabhängigen
Strängen (Datenantworten, HTTP-Input-/Output-Mapping, DB-Abfragen, Studio-Mapper,
OpenAPI-Import, `.http`-Katalog). Dieser Bericht dokumentiert den aktuell lokal
implementierten und getesteten Strang **„Datenantworten begrenzt lesen"** —
die abnahmerelevante Grundlage für LC-AC-02/04 aus dem Plan Z. 244/254.

## Was implementiert ist (lokal, uncommittet)

**Bounded Response-Reader** (`HttpDataResponseReader.cs` + `HttpDataResponse.cs`):
- Liest die HTTP-Response begrenzt als JSON **oder Text** (Size-Limit via
  Stream-Chunk-Read, kein unbeschränkter Variablenwert).
- Liefert **Status, freigegebene Header und Body getrennt** (Success/Statuscode
  im Ergebnis, Content-Type/Encoding erkannt, Felder separat).
- Content-Type-/Encoding-Erkennung aus `CharSet`.
- **Definierte Fehlercodes ohne Teilzuordnung**: `responseTooLarge`
  (zu große Antwort → Abbruch), `invalidBody` (malformiertes JSON),
  `nonJsonText` (kein verarbeitbares JSON). Timeout wird weiterhin vom
  bestehenden CancellationToken/Retry-Mechanismus abgedeckt.
- **Tiefe (rekursive) Redaction** über `ConnectorRedactionPolicy.RedactValue`:
  sensitive skalare und verschachtelte Felder (Top-Level, `nested.apiKey`,
  Listen-Elemente) → `***`.

**Executor-Integration** (`HttpConnectorExecutor.cs`): Der Executor liest nach
dem Request die Response-Body über den Reader und exponierte
- `httpContentType`, `httpBody` und `httpBody.<key>` je Feld als
  Prozessvariablen (unter `connector.output.*`-Ebene über den bestehenden
  Output-Fluss). Erfolg (2xx) → definierter Erfolg mit Felder; nicht lesbare/
  zu große Antwort → definierter Fehler ohne Teilzuordnung via
  `ServiceTaskExecutionException`.

**DI-Verdrahtung** (`ServiceTaskRegistryExtensions.cs`): `HttpDataResponseReader`
und `HttpConnectorExecutor` (mit Reader) per DI registriert; Größenlimit
`ConnectorRuntime:MaxResponseBytes` (Default 256 KiB).

## Was verifiziert ist

- Build: **0 Errors, 0 Warnings.**
- `HttpDataResponseReaderTests` **4/4 grün**: bounded JSON lesen + tiefe Redaction
  verschachtelter/sensibler Felder (`customer`→`A`, `amount`→`12.5`,
  `secretToken`→`***`, `nested.apiKey`→`***`, `list[0].token`→`***`),
  zu große Antwort abgelehnt, malformiertes JSON abgelehnt, Nicht-JSON-Text
  abgelehnt.
- Neuer End-to-End-Executor-Test `HttpExecutor_ExposesJsonResponseFieldsWithRedaction`:
  JSON-Response → Felder als `httpBody.*` in den Outputs inkl. Redaction — grün.
- Alle Connector-/Importer-Tests (ConnectorRuntimeTests inkl. neuer
  Sicherheits-/Versions-Tests, OpenApiConnectorTemplateImporter) weiterhin grün;
  keine Regression.

## Was noch fehlt (Abnahmelücken — bewusst nicht als fertig markiert)

1. **HTTP-Input-Mapping** (Plan Z. 245): Pfad/Query/Header/strukturierter Body
   aus dem Binding auf den Request abbilden; keine ungeprüfte Stringinterpolation
   in JSON/SQL/URLs. (Teilweise durch LC02-Binding-Grundlage adressiert, nicht
   vollständig.)
2. **DB-Abfrage/-Schreiboperation** (Z. 246): parametrisierte, administrativ
   freigegebene Queries, festes Ergebnisschema, Limits, Read-only-Rollen,
   serverseitige Paging-Strategie.
3. **Studio-Mapper** (Z. 247): Eingabe-/Ausgabeschema, Feldwahl, explizite
   Transformation, Beispieldaten, Vorschau; dokumentierte Semantik für
   Null/fehlendes Feld/Datum/Dezimal/Liste/Objekt.
4. **OpenAPI-Import-Ausbau** (Z. 248): Parameter-/Bodies/Responses; nicht
   unterstützte Referenzen/Auth-/Schemaformen als Reviewbedarf kennzeichnen.
5. **Durchgehender Datenfall** (Z. 249): Kundenantwort lesen → Betrag/Kundengruppe
   mappen → DMN auswerten → tatsächlicher BPMN-Pfad — **nicht** nur lokale
   Unit-Tests (kein ausgeführter Studio→Deploy→Start→Request→Folgeschritt-Nachweis).
6. **`.http`-Katalog-Erweiterung** (Z. 250): Datenquellen-/Mappingrequests,
   Beispielantworten ohne sensible Daten, Schema-/Limit-/Timeoutfehler.

## Nächste Schritte (vorgeschlagene Reihenfolge)

1. Teilfortschritt des gelesenen-Response-Strangs committen (klar nicht-fertig
   markiert), damit die Arbeit gesichert ist.
2. HTTP-Input-Mapping (Path/Query/Header/strukturierter Body) anpacken —
   nächster LC03-Teilstrang.
3. Danach DB-Abfrage-Operation und Studio-Mapper; der durchgehende Datenfall
   (nur nach vollständigem Input- und Mapper-Strang) benötigt schließlich einen
   echten Studio→Runtime-Nachweis.

Kein Cloud-Deployment, keine Produktionsänderung erforderlich für die
verbleibende lokale Arbeit.
