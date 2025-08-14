# VertexBPMN MCP-Server

## Überblick

Der MCP-Server ermöglicht die Anbindung der VertexBPMN-Engine an MCP-fähige Tools und AI-Agents (z.B. OpenAI MCP-Clients) über TCP/WebSocket und JSON-RPC 2.0.

- **Transport:** TCP, WebSocket, HTTP
- **Protokoll:** MCP (Model Context Protocol, [Spec](https://github.com/modelcontextprotocol))
- **Architektur:** Saubere Trennung von Engine-Core und MCP-Adapter
- **Serialisierung:** System.Text.Json (camelCase, null-ignore)
- **Logging:** Serilog

## MCP-Methoden

- `bpmn.listProcesses` – Liste aller BPMN-Definitionen
- `bpmn.startInstance` – Startet eine neue Prozessinstanz
- `bpmn.getInstanceState` – Status einer laufenden Instanz
- `bpmn.instanceEvent` – Streamt Events zu laufenden Instanzen

## Starten des Servers

```bash
cd VertexBPMN.McpAdapter
 dotnet run
```


## Beispiele

### 1. Event-Stream (WebSocket)
```csharp
var ws = new ClientWebSocket();
await ws.ConnectAsync(new Uri("ws://localhost:5000/mcp/ws"), CancellationToken.None);
// Event-Subscription
var sub = new { jsonrpc = "2.0", id = 1, method = "bpmn.instanceEvent", @params = new { instanceId = "abc123" } };
await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sub)), WebSocketMessageType.Text, true, CancellationToken.None);
// Empfange Events
var buffer = new byte[4096];
var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
var evtJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
Console.WriteLine(evtJson); // z.B. { "type": "ProcessCompleted", ... }
```

### 2. Fehlerfall (Method not found)
```json
{
	"jsonrpc": "2.0",
	"id": 99,
	"method": "bpmn.unknownMethod"
}
// Response:
{
	"jsonrpc": "2.0",
	"id": 99,
	"error": {
		"code": -32601,
		"message": "Method not found",
		"data": { "method": "bpmn.unknownMethod" }
	}
}
```

### 3. Authentifizierung (JWT)
```http
GET /mcp/ws HTTP/1.1
Host: localhost:5000
Authorization: Bearer <JWT-Token>
```
Fehlende/ungültige Tokens führen zu HTTP 401.

### 4. Monitoring/Telemetry
OpenTelemetry ist aktiviert. Beispiel für Custom Metric:
```csharp
using OpenTelemetry.Metrics;
var meter = MeterProvider.Default.GetMeter("VertexBPMN.McpAdapter");
var counter = meter.CreateCounter<long>("bpmn_process_starts");
counter.Add(1);
```

## Testen

```bash
cd VertexBPMN.McpAdapter.Tests
 dotnet test
```

## Erweiterung

- Eigene BPMN-Methoden können einfach in `McpServer.cs` ergänzt werden.
- Die Engine bleibt unabhängig von MCP nutzbar.

## Lizenz
MIT
