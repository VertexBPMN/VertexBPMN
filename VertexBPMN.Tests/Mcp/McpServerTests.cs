using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using System.Threading;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VertexBPMN.Tests.Mcp;

[Trait("Category", "Integration")]
[Trait("Status", "Disabled")]
public class McpServerTests : IClassFixture<WebApplicationFactory<VertexBPMN.McpAdapter.Program>>
{
    private readonly WebApplicationFactory<VertexBPMN.McpAdapter.Program> _factory;
    private const string DummyJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJ2ZXJ0ZXhibXBuLWFwaSIsImlzcyI6Imh0dHBzOi8veW91ci1vaWRjLXByb3ZpZGVyIiwic3ViIjoiVGVzdFVzZXIiLCJleHAiOjQ3OTk2ODQwMDB9.1QwQvQwQvQwQvQwQvQwQvQwQvQwQvQwQvQwQvQwQ";

    public McpServerTests(WebApplicationFactory<VertexBPMN.McpAdapter.Program> factory)
    {
        _factory = factory;
    }

    [Fact(Skip = "Needs external service")]
    public async Task ListProcesses_JsonRpc_ReturnsProcessList()
    {
        var client = _factory.CreateClient();
        var req = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "bpmn.listProcesses"
        };
        var resp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("invoice", json);
    }

    [Fact(Skip = "Needs external service")]
    public async Task StartInstance_JsonRpc_ReturnsInstanceId()
    {
        var client = _factory.CreateClient();
        var req = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "bpmn.startInstance",
            @params = new { processKey = "invoice", variables = new { amount = 100 } }
        };
        var resp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("instanceId", json);
    }

    [Fact(Skip = "Needs external service")]
    public async Task GetInstanceState_JsonRpc_ReturnsState()
    {
        var client = _factory.CreateClient();
        // Start instance first
        var startReq = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "bpmn.startInstance",
            @params = new { processKey = "invoice", variables = new { amount = 100 } }
        };
        var startResp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(startReq), Encoding.UTF8, "application/json"));
        var startJson = await startResp.Content.ReadAsStringAsync();
        var instanceId = JsonDocument.Parse(startJson).RootElement.GetProperty("result").GetProperty("instanceId").GetString();

        var stateReq = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "bpmn.getInstanceState",
            @params = new { instanceId }
        };
        var stateResp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(stateReq), Encoding.UTF8, "application/json"));
        var stateJson = await stateResp.Content.ReadAsStringAsync();
        Assert.Contains("running", stateJson);
    }

    [Fact(Skip = "Needs external service")]
    public async Task WebSocket_ListProcesses_ReturnsProcessList()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/mcp/ws"), CancellationToken.None);
        var req = new
        {
            jsonrpc = "2.0",
            id = 5,
            method = "bpmn.listProcesses"
        };
        var reqJson = JsonSerializer.Serialize(req);
        var reqBytes = Encoding.UTF8.GetBytes(reqJson);
        await ws.SendAsync(new ArraySegment<byte>(reqBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var respJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Assert.Contains("invoice", respJson);
    }

    [Fact(Skip = "Needs external service")]
    public async Task WebSocket_InstanceEventStream_EmitsAllEventTypes()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/mcp/ws"), CancellationToken.None);

        // Starte eine Instanz
        var startReq = new
        {
            jsonrpc = "2.0",
            id = 10,
            method = "bpmn.startInstance",
            @params = new { processKey = "invoice", variables = new { amount = 100 } }
        };
        var startJson = JsonSerializer.Serialize(startReq);
        var startBytes = Encoding.UTF8.GetBytes(startJson);
        await ws.SendAsync(new ArraySegment<byte>(startBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var respJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var instanceId = JsonDocument.Parse(respJson).RootElement.GetProperty("result").GetProperty("instanceId").GetString();

        // Abonniere Event-Stream
        var eventReq = new
        {
            jsonrpc = "2.0",
            id = 11,
            method = "bpmn.instanceEvent",
            @params = new { instanceId }
        };
        var eventJson = JsonSerializer.Serialize(eventReq);
        var eventBytes = Encoding.UTF8.GetBytes(eventJson);
        await ws.SendAsync(new ArraySegment<byte>(eventBytes), WebSocketMessageType.Text, true, CancellationToken.None);

        // Simuliere Statusänderungen (z.B. Task completed, Progress, Incident, Process completed)
        for (int i = 0; i < 5; i++)
        {
            var stateReq = new
            {
                jsonrpc = "2.0",
                id = 12 + i,
                method = "bpmn.getInstanceState",
                @params = new { instanceId }
            };
            var stateJson = JsonSerializer.Serialize(stateReq);
            var stateBytes = Encoding.UTF8.GetBytes(stateJson);
            await ws.SendAsync(new ArraySegment<byte>(stateBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            await Task.Delay(100);
        }

        // Sammle bis zu 20 Events und prüfe auf alle Typen
        var receivedEvents = new List<string>();
        var foundTypes = new HashSet<string>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (int i = 0; i < 20; i++)
        {
            try
            {
                var evtResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                var evtJson = Encoding.UTF8.GetString(buffer, 0, evtResult.Count);
                receivedEvents.Add(evtJson);
                if (evtJson.Contains("ProcessCompleted")) foundTypes.Add("ProcessCompleted");
                if (evtJson.Contains("TaskCompleted")) foundTypes.Add("TaskCompleted");
                if (evtJson.Contains("Incident")) foundTypes.Add("Incident");
                if (evtJson.Contains("ProgressUpdate")) foundTypes.Add("ProgressUpdate");
                if (foundTypes.Count == 4) break;
            }
            catch (OperationCanceledException)
            {
                // Timeout erreicht, Schleife abbrechen
                break;
            }
        }

        Assert.Contains("ProcessCompleted", foundTypes);
        Assert.Contains("TaskCompleted", foundTypes);
        Assert.Contains("Incident", foundTypes);
        Assert.Contains("ProgressUpdate", foundTypes);
    }

    [Fact(Skip = "Needs external service")]
    public async Task StartInstance_WithLargePayload_Works()
    {
        var client = _factory.CreateClient();
        var variables = new Dictionary<string, object>();
        for (int i = 0; i < 1000; i++)
            variables[$"var{i}"] = new string('x', 1000);
        var req = new
        {
            jsonrpc = "2.0",
            id = 100,
            method = "bpmn.startInstance",
            @params = new { processKey = "invoice", variables }
        };
        var resp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("instanceId", json);
    }

    [Fact(Skip = "Needs external service")]
    public async Task StartMultipleInstances_Parallel_Works()
    {
        var client = _factory.CreateClient();
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 10; i++)
        {
            var req = new
            {
                jsonrpc = "2.0",
                id = 200 + i,
                method = "bpmn.startInstance",
                @params = new { processKey = "invoice", variables = new { amount = i * 10 } }
            };
            tasks.Add(Task.Run(async () => {
                var resp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));
                var json = await resp.Content.ReadAsStringAsync();
                return json;
            }));
        }
        var results = await Task.WhenAll(tasks);
        foreach (var json in results)
            Assert.Contains("instanceId", json);
    }

    [Fact(Skip = "Needs external service")]
    public async Task InvalidMethod_ReturnsError()
    {
        var client = _factory.CreateClient();
        var req = new
        {
            jsonrpc = "2.0",
            id = 300,
            method = "bpmn.unknownMethod"
        };
        var resp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("error", json);
        Assert.Contains("Method not found", json);
    }

    [Fact(Skip = "Needs external service")]
    public async Task StartInstance_WithInvalidParams_ReturnsError()
    {
        var client = _factory.CreateClient();
        var req = new
        {
            jsonrpc = "2.0",
            id = 400,
            method = "bpmn.startInstance",
            @params = new { processKey = "invoice" } // variables fehlen
        };
        var resp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("error", json);
    }

    // Beispiel für echten BPMN-Prozess (hier nur Dummy, kann mit echtem Modell erweitert werden)
    [Fact(Skip = "Needs external service")]
    public async Task StartInstance_WithComplexModel_Works()
    {
        var client = _factory.CreateClient();
        var variables = new { amount = 100, customer = "Test", items = new[] { "A", "B", "C" } };
        var req = new
        {
            jsonrpc = "2.0",
            id = 500,
            method = "bpmn.startInstance",
            @params = new { processKey = "complexProcess", variables }
        };
        var resp = await client.PostAsync("/mcp/jsonrpc", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("instanceId", json);
    }

    [Fact(Skip = "Needs external service")]
    public async Task WebSocket_ListProcesses_ReturnsProcessList_TestBypass()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/mcp/ws"), CancellationToken.None);
        var req = new
        {
            jsonrpc = "2.0",
            id = 5,
            method = "bpmn.listProcesses"
        };
        var reqJson = JsonSerializer.Serialize(req);
        var reqBytes = Encoding.UTF8.GetBytes(reqJson);
        await ws.SendAsync(new ArraySegment<byte>(reqBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var respJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Assert.Contains("invoice", respJson);
    }

    [Fact(Skip = "Needs external service")]
    public async Task WebSocket_InstanceEventStream_EmitsAllEventTypes_TestBypass()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/mcp/ws"), CancellationToken.None);

        // Starte eine Instanz
        var startReq = new
        {
            jsonrpc = "2.0",
            id = 10,
            method = "bpmn.startInstance",
            @params = new { processKey = "invoice", variables = new { amount = 100 } }
        };
        var startJson = JsonSerializer.Serialize(startReq);
        var startBytes = Encoding.UTF8.GetBytes(startJson);
        await ws.SendAsync(new ArraySegment<byte>(startBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var respJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var instanceId = JsonDocument.Parse(respJson).RootElement.GetProperty("result").GetProperty("instanceId").GetString();

        // Abonniere Event-Stream
        var eventReq = new
        {
            jsonrpc = "2.0",
            id = 11,
            method = "bpmn.instanceEvent",
            @params = new { instanceId }
        };
        var eventJson = JsonSerializer.Serialize(eventReq);
        var eventBytes = Encoding.UTF8.GetBytes(eventJson);
        await ws.SendAsync(new ArraySegment<byte>(eventBytes), WebSocketMessageType.Text, true, CancellationToken.None);

        // Simuliere Statusänderungen (z.B. Task completed, Progress, Incident, Process completed)
        for (int i = 0; i < 5; i++)
        {
            var stateReq = new
            {
                jsonrpc = "2.0",
                id = 12 + i,
                method = "bpmn.getInstanceState",
                @params = new { instanceId }
            };
            var stateJson = JsonSerializer.Serialize(stateReq);
            var stateBytes = Encoding.UTF8.GetBytes(stateJson);
            await ws.SendAsync(new ArraySegment<byte>(stateBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            await Task.Delay(100);
        }

        // Sammle bis zu 20 Events und prüfe auf alle Typen
        var receivedEvents = new List<string>();
        var foundTypes = new HashSet<string>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (int i = 0; i < 20; i++)
        {
            try
            {
                var evtResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                var evtJson = Encoding.UTF8.GetString(buffer, 0, evtResult.Count);
                receivedEvents.Add(evtJson);
                if (evtJson.Contains("ProcessCompleted")) foundTypes.Add("ProcessCompleted");
                if (evtJson.Contains("TaskCompleted")) foundTypes.Add("TaskCompleted");
                if (evtJson.Contains("Incident")) foundTypes.Add("Incident");
                if (evtJson.Contains("ProgressUpdate")) foundTypes.Add("ProgressUpdate");
                if (foundTypes.Count == 4) break;
            }
            catch (OperationCanceledException)
            {
                // Timeout erreicht, Schleife abbrechen
                break;
            }
        }

        Assert.Contains("ProcessCompleted", foundTypes);
        Assert.Contains("TaskCompleted", foundTypes);
        Assert.Contains("Incident", foundTypes);
        Assert.Contains("ProgressUpdate", foundTypes);
    }
}
