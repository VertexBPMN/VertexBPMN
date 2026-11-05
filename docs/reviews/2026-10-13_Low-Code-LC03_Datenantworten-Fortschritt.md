# LC03 Datenantworten Abfragen und visueller Mapper — Fortschrittsbericht

Stand: 2026-10-14 · Branch `codex/low-code-lc00` · Paket L (abhängig von LC02)

## Status: offen — Teilfortschritt, noch nicht abnahmefähig

LC03 ist ein großes Paket mit mehreren, teils voneinander unabhängigen
Strängen (Datenantworten, HTTP-Input-/Output-Mapping, DB-Abfragen, Studio-Mapper,
OpenAPI-Import, `.http`-Katalog). Dieser Bericht dokumentiert die aktuell lokal
implementierten und getesteten Stränge.

## Was implementiert ist (lokal, committet als Teilfortschritt)

### Strang A — Datenantworten begrenzt lesen (LC-AC-02/04-Grundlage)
- **`HttpDataResponseReader`** (`HttpDataResponse.cs`): liest die HTTP-Response
  begrenzt als JSON **oder** Text (Size-Limit via Stream-Chunk-Read, kein
  unbeschränkter Variablenwert). Content-Type/Encoding aus `CharSet`; Status/Header/
  Body getrennt. **Definierte Fehlercodes ohne Teilzuordnung**: `bodyTooLarge`,
  `invalidBody`, `nonJsonText`.
- **Tiefe Redaction** (`ConnectorRedactionPolicy.RedactValue`): sensitive und
  verschachtelte Felder (Top-Level, `nested.apiKey`, Listen-Elemente) → `***`.
- **Executor-Integration** (`HttpConnectorExecutor`): exponiert `httpContentType`,
  `httpBody`, `httpBody.<field>` als Prozessvariablen; nicht lesbare/zu große
  Antwort → definierter Fehler ohne Teilzuordnung.
- **DI**: Reader + Executor registriert; Limit `ConnectorRuntime:MaxResponseBytes`
  (Default 256 KiB).

### Strang B — HTTP-Input-Mapping (Path/Query/Header/Body, kein unchecked Splice)
- **Query-Mapping**: `vertex:connector.query.<name>` → URL-Query (resolved +
  percent-encoded).
- **Strukturierter Body**: `vertex:connector.bodyField.<name>` → JSON-Objekt
  (resolved, JSON-escaped); Legacy-`vertex:connector.body` weiter unterstützt
  (jetzt auch resolved).
- **`ConnectorValueResolver` verdrahtet** in Query/Header/Body: `{{var}}`-Referenzen
  lösen gegen Prozessvariablen auf; unaufgelöste Referenzen → fail-fast
  (`ServiceTaskExecutionException`), keine Teilzuordnung.

### Strang C — OpenAPI-Import-Ausbau (Parameter/Bodies auf Executor-Konvention)
- Query-Params → `vertex:connector.query.*`; flache JSON-Objekte → `vertex:connector.bodyField.*`;
  nested/komplexe Formen bleiben `body` und flaggen `NeedsReview`.
- OAuth2 weiterhin manuell zu verknüpfen (Review); nicht unterstützte Formen
  explizit als Reviewbedarf gekennzeichnet (Z.248).

### Strang D — `.http`-Katalog (Z.250, Teil)
- `connectors-and-mapping.http` um LC03-Requests erweitert: Query-/Body-Mapping,
  Beispielantworten ohne sensible Daten, Schema-/Limit-/Timeoutfehler.

### Strang E — DB-Abfrage-Operation (Z.246, Lesepfad)
- `DatabaseConnectorExecutor` bekommt einen **query-Modus** (`vertex:connector.mode=query`):
  führt ein einzelnes, **read-only SELECT** aus und liefert das Ergebnis strukturiert
  als `rows` (`List<Dictionary<string, object?>>`) plus `rowCount`; Integer-Skalare
  werden auf `long` normalisiert.
- **Serverseitiges Zeilenlimit** (`vertex:connector.rowLimit`, Default 100, Cap 1000).
- **Harte Guards**: nicht-SELECT und mehrteilige (`;`) Befehle werden abgelehnt —
  kein beliebiges Designer-SQL im Lesepfad. Schreibpfad (`ExecuteNonQuery`/
  `affectedRows`) unverändert.
- Tests (TDD): strukturierte `rows`/`rowCount`, Zeilenlimit-Cap, Ablehnung nicht-SELECT /
  gestapelter Anweisungen. SQLite-basiert, lokal grün.

### Strang F — Studio-Mapper (Z.247)
- **`FieldMapper`** (Application): explizite Feldzuordnungen (`FieldMapping`) mit
  Dotted-Pfaden (verschachtelte Dict/Liste). **Fail-closed**: fehlendes Pflichtfeld oder
  fehlgeschlagene explizite Transformation bricht die gesamte Zuordnung ab (keine
  Teilzuordnung). Transformationen `Copy`/`ToText`/`ToNumber`/`ToDate`/`ToBool`.
- **`FieldMappingPreviewService`** (Application): parst Beispiel-JSON und wendet die
  Zuordnungen an — isoliert testbar.
- **`MappingController`** (API): `POST /api/mapping/preview` — stateless, Policy
  `ProcessManager`.
- **Studio** (`FieldMapperPanel.razor` + `HttpFieldMappingService`): Eingabe-/Ausgabe-
  schema, Feldwahl (Quelle/Ziel), explizite Transformation, Pflicht-Flag, Beispieldaten-
  Textarea und Vorschau mit Ergebnis-/Fehleranzeige.
- **Dokumentierte Semantik**: `docs/reference/low-code-mapper-semantics.md` (Null/
  fehlendes Feld, Datum, Dezimal, Liste, Objekt, Fehlerverhalten).
- Tests: `FieldMapperTests` 8× + `FieldMappingPreviewServiceTests` 3× grün; Studio +
  API kompilieren.

### Strang G — Mapper-Binding-Integration (Z.247 Rest)
- **Output-Mapping im Executor**: neues Bindungs-Attribut `vertex:connector.outputMapping`
  (JSON-Array von `{sourcePath, targetKey, transform, required}`). Die Runtime wendet
  bei einer echten Datenantwort `FieldMapper` auf die gelesenen Response-Felder an und
  stellt die Zielwerte als Prozessvariablen bereit.
- **Fail-closed**: schlägt das Mapping fehl (fehlendes Pflichtfeld, unbekannter Transform,
  ungültiges JSON), wird die Service-Task-Execution-Exception geworfen, **bevor irgendein
  Output exponiert wird** — keine Teilzuordnung (auch nicht `httpBody.*`).
- Transform-Werte werden case-insensitive geparst; unbekannter Transform bricht mit
  definiertem Fehler ab (statt still auf `Copy` zu fallen).
- Tests (rot→grün, TDD): `AppliesOutputMappingFromBinding` (ToText+ToNumber → Zielwerte),
  `RejectsFailedOutputMapping_WithoutPartialExposure` (required-Missing → Exception),
  plus wiederhergestellter `ExposesJsonResponseFieldsWithRedaction`.

### Strang H — Durchgehender Datenfall (Z.249)
Echter End-to-End-Datenfall: Kundenantwort → Output-Mapping → Prozessvariablen →
FEEL-Gateway → tatsächlicher BPMN-Pfad. **Keine vorab gesetzten Ergebnisvariablen.**

- `tests/VertexBPMN.Tests/Acceptance/PersistentEndToEndDmnDataFlowTests.cs` (neu):
  lokaler TCP-Mock-HTTP-Server (`/customer`, `/gold`, `/default`), reale
  Connector-Pipeline (`HttpConnectorExecutor` + `ConnectorRegistry` +
  `ConnectorRuntime` + `VertexConnectorServiceTaskHandler`), reale
  `PersistentProcessExecutionRuntime`, FEEL-Exclusive-Gateway auf
  `connector.output.betrag > 1000`, Pfad-Markierung pro Zweig (nur der gewählte
  Branch führt seinen Marker-Connector aus). `[Theory]` 2 Fälle:
  - Betrag **1500** → GOLD-Zweig, `kundengruppe=gold`
  - Betrag **300** → DEFAULT-Zweig, `kundengruppe=standard`
- Alle Variablen (`connector.output.betrag|kundengruppe|branch`) entstehen aus der
  echten HTTP-Antwort über das Output-Mapping (Strang G) — nichts vorab gesetzt.
- **Namespace-Fix (Pitfall):** vertex-Extensions erfordern den kanonischen
  Namespace `https://vertexbpmn.io/schema/bpmn/1.0` (`VertexBpmnExtensions.NamespaceUri`);
  `http://...` (ohne https) wird nicht erkannt → `node.Implementation` bleibt leer
  → Suspended mit „No service-task handler is registered for ''“.

## Was verifiziert ist

- Build: **0 Errors, 0 Warnings.**
- `HttpDataResponseReaderTests` **4/4 grün**.
- 4 neue Input-Mapping-Tests (Query + Var-Auflösung, unaufgelöste Query abgelehnt,
  strukturierter Body gebaut, unaufgelöster Body-Feld abgelehnt) grün.
- 2 neue Importer-Tests (Query/BodyField-Mapping, komplexer Body + Review) grün.
- 3 neue DB-Query-Tests (strukturierte rows/rowCount, Zeilenlimit-Cap, read-only
  Guard) + bestehender SQLite-Parameter-Test grün.
- Volle Connector-/Importer-Suite grün, keine Regression.
- **Strang H (Z.249):** 2 neue End-to-End-Datenfall-Tests grün (GOLD 1500, DEFAULT 300);
  Regressionsfilter Connector/FieldMapper/DatabaseConnector/Importer **49/49 grün**.
- Commits local (nicht gepusht): `765b01f5` (A), `6889fbd0` (B), `19f5f8e5` (D),
  `37aaff3b` (C), `8c783328` (E), `cfcce709` (Bericht).

## Was noch fehlt (Abnahmelücken — bewusst nicht als fertig markiert)

1. **DB-Write-Seite (Z.246, Teil)**: `mode=query` (Lesepfad) ist umgesetzt; es fehlen
   Read-only-Rollen-Abnahme (Infra) und die explizite „nur administrativ freigegebene
   Queries"-Konfig (Browser-Designer sendet keine beliebige SQL) — der harte
   SELECT-Guard schützt den Lesepfad bereits.
2. **Studio-Mapper (Z.247, Rest)**: `FieldMapper` + Preview-API + `FieldMapperPanel.razor`
   (Feldwahl, Transformation, Beispieldaten, Vorschau) und die **Integration in den
   Connector-Binding-Flow** (`vertex:connector.outputMapping`, fail-closed im Executor)
   sind implementiert und getestet. Es fehlt nur noch eine **visuelle Laufzeit-Prüfung**
   der Komponente (bisher nur Kompilation verifiziert) und der volle End-to-End-Datenfall.
3. **Sensitive-Felder-Schutz History/Audit (Z.245)**: Design bereits erfüllt
   (Redaction vor Variablen; Audit speichert keine Body-Felder), aber nicht durch
   einen eigenen Test belegt.
4. **Durchgehender Datenfall (Z.249)**: Kundenantwort lesen → Betrag/Kundengruppe
   mappen → DMN auswerten → tatsächlicher BPMN-Pfad — kein ausgeführter
   Studio→Deploy→Start→Request→Folgeschritt-Nachweis (nur Unit-Tests).
5. **DB-, Mapper-Integration im `.http`-Katalog**: nur Konzeptblöcke, kein echter
   Lauf (fehlende Testdienste + Runtime-Nachweis).

## Nächste Schritte (vorgeschlagene Reihenfolge)

1. **Durchgehender Datenfall (Z.249)**: Kundenantwort lesen → Output-Mapping
   (`vertex:connector.outputMapping`, jetzt im Executor aktiv) → Betrag/Kundengruppe als
   Prozessvariablen → DMN auswerten → tatsächlicher BPMN-Pfad. Erfordert einen echten
   Studio→Deploy→Start→Request→Folgeschritt-Nachweis (bisher nur Unit-Tests).
2. Visuelle Laufzeit-Prüfung von `FieldMapperPanel.razor` im Studio (nur Kompilation
   verifiziert bisher).
3. DB-Read-only-/Admin-Approval-Verfeinerung, falls Infra-Zugriff besteht.

Kein Cloud-Deployment, keine Produktionsänderung für die verbleibende lokale Arbeit.

