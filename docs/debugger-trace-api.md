# VertexBPMN: Visueller Debugger & Trace-API

## Trace-API für BPMN-Debugging
- Endpoint: `POST /api/debug/trace`
- Request-Body:
  ```json
  {
    "bpmnXml": "<definitions ...>...</definitions>",
    "variables": { "foo": 42 }
  }
  ```
- Response: Liste der ausgeführten BPMN-Elemente (Trace)
  ```json
  [
    "StartEvent: start1",
    "SequenceFlow: flow1",
    "UserTask: t1",
    ...
  ]
  ```

## Anwendungsfälle
- Visualisierung des Token-Flows im Frontend (bpmn-js, custom UI)
- Unit- und Integrationstests für Prozessmodelle
- Analyse und Debugging von BPMN-Workflows

## Beispiel (curl)
```bash
curl -X POST "http://localhost:5000/api/debug/trace" -H "Content-Type: application/json" -d '{
  "bpmnXml": "<definitions ...>...</definitions>",
  "variables": { "foo": 42 }
}'
```

---
*Letztes Update: August 2025*
