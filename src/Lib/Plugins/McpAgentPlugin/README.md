# MCP-Agent-Plugin für VertexBPMN

## Zweck
Dieses Plugin ermöglicht die Ausführung von Service Tasks mit MCP-Agenten (REST/WebSocket) in der bestehenden BPMN-Engine.

## Registrierung
```csharp
var plugin = new McpAgentPlugin("agents.json", engine.Logger);
engine.RegisterPlugin(plugin);
```

## Beispielprozess
Siehe `ExampleProcess.bpmn`:
- Service Task mit `implementation="mcp-agent"` und `agentName="NLP"`

## Konfiguration
Agenten werden in `agents.json` definiert:
```json
{
  "agents": [
    { "name": "NLP", "type": "REST", "url": "http://localhost:6001/api/nlp" },
    { "name": "Recommender", "type": "REST", "url": "http://localhost:6002/api/recommend" }
  ]
}
```

## Unit-Test
```csharp
var plugin = new McpAgentPlugin("agents.json", testLogger);
engine.RegisterPlugin(plugin);
engine.ExecuteProcess("ExampleProcess.bpmn");
```

## Hinweise
- Keine Änderungen am BPMN-Core nötig
- Fehler und Retries werden geloggt
- Erweiterbar für neue Agenten
