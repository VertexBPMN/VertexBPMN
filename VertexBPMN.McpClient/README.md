# VertexBPMN.McpClient

Client-Bibliothek für MCP-Calls (JSON-RPC und Event-Stream) für VertexBPMN.

## Features
- JSON-RPC-Calls über HTTP
- Event-Stream über WebSocket
- Authentifizierung via JWT (Authorization Header)

## Beispiel
```csharp
using VertexBPMN.McpClient;

var client = new McpClient("https://localhost:5001");
var token = "<JWT-Token>";

// List Processes
var result = await client.CallJsonRpcAsync("bpmn.listProcesses", null, token);
Console.WriteLine(result);

// Start Instance
var startResult = await client.CallJsonRpcAsync("bpmn.startInstance", new { processKey = "invoice", variables = new { amount = 100 } }, token);
Console.WriteLine(startResult);

// Event-Stream
await client.ConnectWebSocketAsync("<instanceId>", token, evt => Console.WriteLine(evt));
```

## NuGet Publishing
- Paketname: `VertexBPMN.McpClient`
- `dotnet pack` und `dotnet nuget push` für Veröffentlichung

## Lizenz
MIT
