# VertexBPMN API Quickstart

Dieses Dokument zeigt, wie du die REST-API von VertexBPMN schnell testen und nutzen kannst.

## Voraussetzungen
- .NET 9 SDK
- Laufende Instanz von VertexBPMN.Api (z.B. via `dotnet run`)

## OpenAPI/Swagger
- Die API ist unter `/swagger` dokumentiert und kann im Browser getestet werden.

## Beispiel: Prozess deployen und starten

### 1. Prozessdefinition deployen
```bash
curl -X POST "http://localhost:5000/api/repository" -H "Content-Type: application/json" -d '{
  "bpmnXml": "<definitions ...>...</definitions>",
  "name": "hello-world.bpmn"
}'
```

### 2. Prozessinstanz starten
```bash
curl -X POST "http://localhost:5000/api/runtime/start" -H "Content-Type: application/json" -d '{
  "ProcessDefinitionKey": "Process_HelloWorld",
  "Variables": { "foo": 42 }
}'
```

### 3. Tasks abfragen
```bash
curl "http://localhost:5000/api/task"
```

### 4. DMN-Entscheidung auswerten
```bash
curl -X POST "http://localhost:5000/api/decision/evaluate" -H "Content-Type: application/json" -d '{
  "DecisionKey": "my-decision",
  "Inputs": { "input1": "value" }
}'
```

## Health-Check
```bash
curl "http://localhost:5000/api/health"
```

## Hinweise
- Alle Endpunkte und Modelle sind in `/swagger` dokumentiert.
- Für produktive Nutzung empfiehlt sich Authentifizierung und HTTPS.

---
*Letztes Update: August 2025*
