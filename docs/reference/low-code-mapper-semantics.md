# Low-Code Mapper — Semantik (LC03 Z.247)

Status: LC03 Teilstrang · Basis für den Studio-Mapper

Der Studio-Mapper bildet **explizite Feldzuordnungen** (`FieldMapping`) auf einen
**Quellwert** (HTTP-Antwort-Body oder DB-Query-Zeilenmenge) ab. Die Zuordnung wird über
die `VertexBPMN.Application.Connectors.FieldMapper`-Klasse angewendet; die Studio-Vorschau
läuft über `POST /api/mapping/preview` (stateless, keine Persistenz).

## Zuordnungsmodell

Ein `FieldMapping` besteht aus:

| Feld | Bedeutung |
|---|---|
| `SourcePath` | Dotted-Pfad in den Quellwert, z. B. `httpBody.customer` oder `rows.0.name` (verschachtelte Dict/Liste). |
| `TargetKey` | Zielvariable/Key, unter der der aufgelöste Wert geschrieben wird. |
| `Transform` | Explizite Transformation: `Copy`, `ToText`, `ToNumber`, `ToDate`, `ToBool`. |
| `Required` | Fehlt/ist eine Pflichtquelle null, bricht die **gesamte** Zuordnung ab (keine Teilzuordnung). |

## Semantik der Werte

- **Null / fehlendes Feld**: Quellfeld fehlt oder ist `null` → der Zielwert wird `null`
  gesetzt, die Transformation wird nicht angewendet (kein Fehler). Nur wenn
  `Required=true` ist, bricht die Zuordnung mit einem Fehler ab.
- **Datum** (`ToDate`): `DateTime` → `DateTimeOffset` (UTC); String wird mit
  `DateTimeOffset.TryParse` (invariant/ISO) geparst. Nicht parsebar → Fehler.
- **Dezimalwert** (`ToNumber`): Zahl oder numerischer String → `decimal` (invariant).
  Nicht parsebar (z. B. `"abc"`) → Fehler.
- **Liste** (`ToNumber`/`ToText`/`ToDate`/`ToBool`): wird abgelehnt (Fehler). Eine Liste
  oder ein Objekt wird **nur** mit `Copy` durchgereicht (unverändert, als `List<object?>`
  bzw. `Dictionary<string, object?>`).
- **Objekt** (`Copy`): wird unverändert als `Dictionary<string, object?>` durchgereicht.
- **Text** (`ToText`): Skalar → invariante String-Darstellung; `null` bleibt `null`.
- **Bool** (`ToBool`): `true`/`false`, `"true"`/`"false"`, `"1"`/`"0"`.

## Fehlerverhalten (fail-closed)

- Ein unauflösbarer Pfad, ein fehlendes Pflichtfeld oder eine fehlgeschlagene
  Transformation erzeugt **einen Fehler je betroffenem Ziel** (mit `TargetKey` und Grund).
- Bei beliebig vielen Fehlern wird **kein Teilwert** zurückgegeben: `Success=false`,
  `Values` ist leer. So gelangen keine halb zugeordneten Variablen in den Prozess.

## Preview-API

`POST /api/mapping/preview` (Policy `ProcessManager`):

```json
{
  "sourceJson": "{ \"customer\": { \"name\": \"A\" }, \"amount\": 12.50 }",
  "mappings": [
    { "sourcePath": "customer.name", "targetKey": "customerName", "transform": "Copy", "required": true },
    { "sourcePath": "amount", "targetKey": "amount", "transform": "ToNumber", "required": false }
  ]
}
```

Antwort 200:

```json
{ "success": true, "values": { "customerName": "A", "amount": 12.5 }, "errors": [] }
```

Bei Fehler z. B.:

```json
{ "success": false, "values": {}, "errors": [ "'x': source 'customer.missing' is missing or null." ] }
```

## Laufzeitverwendung

Der Mapper ist der Baustein, auf dem der Studio-Mapper (Eingabe-/Ausgabeschema, Feldwahl,
explizite Transformation, Beispieldaten, Vorschau) aufbaut. Die Vorschau ist bewusst
stateless; die eigentliche Zuweisung von Prozessvariablen aus einer echten Datenantwort
erfolgt über den Connector-Output-Fluss (Redaction vor Übernahme, keine sensiblen Felder
in History/Audit).
