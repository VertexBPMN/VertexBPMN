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

## Was verifiziert ist

- Build: **0 Errors, 0 Warnings.**
- `HttpDataResponseReaderTests` **4/4 grün**.
- 4 neue Input-Mapping-Tests (Query + Var-Auflösung, unaufgelöste Query abgelehnt,
  strukturierter Body gebaut, unaufgelöster Body-Feld abgelehnt) grün.
- 2 neue Importer-Tests (Query/BodyField-Mapping, komplexer Body + Review) grün.
- Volle Connector-/Importer-Suite grün, keine Regression.
- Commits local (nicht gepusht): `765b01f5` (A), `6889fbd0` (B), `19f5f8e5` (D), `37aaff3b` (C).

## Was noch fehlt (Abnahmelücken — bewusst nicht als fertig markiert)

1. **DB-Abfrage/-Schreiboperation (Z.246)**: `DatabaseConnectorExecutor` ist derzeit
   reiner Schreibpfad (`ExecuteNonQuery`, `affectedRows`). Es fehlen: DB-Abfrage als
   eigene Operation mit festem Ergebnisschema (Zeilen/Spalten als Variablen),
   serverseitige Limits + Paging, Read-only-Rollen-Abnahme, „Browser-Designer sendet
   keine beliebige SQL" (nur administrativ freigegebene Queries). Großer eigener Strang.
2. **Studio-Mapper (Z.247)**: Eingabe-/Ausgabeschema, Feldwahl, explizite
   Transformation, Beispieldaten, Vorschau im Studio (Razor). UI-Arbeit.
3. **Sensitive-Felder-Schutz History/Audit (Z.245)**: Design bereits erfüllt
   (Redaction vor Variablen; Audit speichert keine Body-Felder), aber nicht durch
   einen eigenen Test belegt.
4. **Durchgehender Datenfall (Z.249)**: Kundenantwort lesen → Betrag/Kundengruppe
   mappen → DMN auswerten → tatsächlicher BPMN-Pfad — kein ausgeführter
   Studio→Deploy→Start→Request→Folgeschritt-Nachweis (nur Unit-Tests).
5. **DB-, Mapper-Integration im `.http`-Katalog**: nur Konzeptblöcke 10–14, kein
   echter Lauf (fehlende Testdienste + Runtime-Nachweis).

## Nächste Schritte (vorgeschlagene Reihenfolge)

1. **DB-Abfrage-Operation** (Z.246): Query-Read mit festem Ergebnisschema + Limits
   im `DatabaseConnectorExecutor` (lokal testbar).
2. Studio-Mapper (Z.247) — UI, größerer Aufwand.
3. Durchgehender Datenfall (Z.249) — erfordert Input- + Mapper-Strang vollständig
   und schließlich einen echten Studio→Runtime-Nachweis.

Kein Cloud-Deployment, keine Produktionsänderung für die verbleibende lokale Arbeit.

