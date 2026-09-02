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


## Persistenter Visual-Debug-Step-over
- Endpoint: `POST /api/visual-debugger/instance/{processInstanceId}/step`
- Berechtigung: authentifizierter Zugriff; der Prozessinstanz-Tenant muss zum Benutzerkontext passen.
- Verhalten: bewegt genau einen gespeicherten `ExecutionToken` entlang des nächsten `SequenceFlow`, aktualisiert Prozessinstanz und Token, schreibt ein `VISUAL_DEBUG_STEP_OVER`-History-Event und liefert den neuen Zustand zurück.
- Wiederholte Aufrufe arbeiten auf dem gespeicherten Zustand. Beim Erreichen eines `endEvent` werden Token und Prozessinstanz als abgeschlossen markiert.
- Bei fehlender Definition, fehlendem Start-/Ausgangsfluss oder bereits abgeschlossener Instanz liefert die API einen fachlichen Fehler statt eines simulierten Erfolgs.

Beispiel:
```bash
curl -X POST "http://localhost:5000/api/visual-debugger/instance/{processInstanceId}/step" \
  -H "Authorization: Bearer $TOKEN"
```

## Persistente Prozessvisualisierung
- Endpoint: `GET /api/visual-debug/visualize/{processInstanceId}`
- Berechtigung: authentifizierter Zugriff; der Prozessinstanz-Tenant muss zum Benutzerkontext passen.
- Die Antwort enthält die gespeicherte BPMN-Definition, aktive ExecutionTokens, aus HistoryEvents abgeleitete abgeschlossene Aktivitäten und Laufzeitmetriken.
- Aktive Token werden nicht simuliert. Ihre Position entspricht `CurrentNodeId`, ihr Status dem gespeicherten Tokenzustand.
- Die Metriken basieren auf der BPMN-Flow-Node-Struktur sowie auf dem tatsächlichen Instanzzeitraum; fehlende persistierte Daten werden nicht durch Demo-Werte ersetzt.

Beispiel:
```bash
curl "http://localhost:5000/api/visual-debug/visualize/{processInstanceId}" \
  -H "Authorization: Bearer $TOKEN"
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
