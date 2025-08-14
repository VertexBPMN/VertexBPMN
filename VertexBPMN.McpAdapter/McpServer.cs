using System.Threading.Channels;
using VertexBPMN.Core;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
public class McpServer
{

    private JsonElement BuildError(int code, string message, object? data = null)
    {
        var errorObj = new
        {
            code,
            message,
            data
        };
        return JsonSerializer.SerializeToElement(errorObj);
    }

    private JsonElement BuildStatus(string status, string? detail = null, object? data = null)
    {
        var statusObj = new
        {
            status,
            detail,
            data
        };
        return JsonSerializer.SerializeToElement(statusObj);
    }
    private readonly IBpmnEngine _engine;
    private readonly ILogger<McpServer> _logger;
    private readonly ConcurrentDictionary<string, Channel<JsonElement>> _eventChannels = new();

    public McpServer(IBpmnEngine engine, ILogger<McpServer> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public ChannelReader<JsonElement>? GetInstanceEventStream(string instanceId)
    {
        if (_eventChannels.TryGetValue(instanceId, out var channel))
            return channel.Reader;
        return null;
    }

    public async Task HandleJsonRpcAsync(string json, Func<JsonElement, Task> sendResponse, Func<JsonElement, Task>? broadcastEvent = null)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idProp) ? idProp : default;
        var method = root.GetProperty("method").GetString();
        var @params = root.TryGetProperty("params", out var p) ? p : default;

        try
        {
            switch (method)
            {
                case "bpmn.listProcesses":
                    var processes = _engine.ListProcesses();
                    await sendResponse(JsonSerializer.SerializeToElement(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = processes,
                        status = BuildStatus("ok", "Process list fetched")
                    }));
                    break;
                case "bpmn.startInstance":
                    var key = @params.GetProperty("processKey").GetString();
                    var variables = @params.GetProperty("variables").Deserialize<Dictionary<string, object>>();
                    var instanceId = await _engine.StartInstanceAsync(key!, variables!);
                    // Demo: Sende alle Event-Typen in den Event-Channel, falls abonniert
                    if (_eventChannels.TryGetValue(instanceId, out var eventChan))
                    {
                        await eventChan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "ProcessCompleted", instanceId, timestamp = DateTime.UtcNow }));
                        await eventChan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "TaskCompleted", instanceId, timestamp = DateTime.UtcNow }));
                        await eventChan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "Incident", instanceId, timestamp = DateTime.UtcNow }));
                        await eventChan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "ProgressUpdate", instanceId, timestamp = DateTime.UtcNow }));
                    }
                    await sendResponse(JsonSerializer.SerializeToElement(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new { instanceId },
                        status = BuildStatus("started", $"Instance {instanceId} started", new { processKey = key })
                    }));
                    break;
                case "bpmn.instanceEvent":
                    // Event streaming: subscribe client to instance event channel
                    var instId = @params.GetProperty("instanceId").GetString();
                    if (!_eventChannels.ContainsKey(instId!))
                    {
                        _eventChannels[instId!] = Channel.CreateUnbounded<JsonElement>();
                    }
                    // Demo: Sende alle Event-Typen direkt nach Subscription
                    if (_eventChannels.TryGetValue(instId!, out var subChan))
                    {
                        await subChan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "ProcessCompleted", instanceId = instId, timestamp = DateTime.UtcNow }));
                        await subChan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "TaskCompleted", instanceId = instId, timestamp = DateTime.UtcNow }));
                        await subChan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "Incident", instanceId = instId, timestamp = DateTime.UtcNow }));
                        await subChan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "ProgressUpdate", instanceId = instId, timestamp = DateTime.UtcNow }));
                    }
                    await sendResponse(JsonSerializer.SerializeToElement(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = "subscribed",
                        status = BuildStatus("ok", $"Subscribed to instance {instId}")
                    }));
                    break;
                case "bpmn.sendInstanceEvent":
                    // For testing: send a custom event to the instance channel
                    var targetId = @params.GetProperty("instanceId").GetString();
                    var eventType = @params.GetProperty("type").GetString();
                    if (_eventChannels.TryGetValue(targetId!, out var chan))
                    {
                        await chan.Writer.WriteAsync(JsonSerializer.SerializeToElement(new
                        {
                            type = eventType,
                            instanceId = targetId,
                            timestamp = DateTime.UtcNow
                        }));
                    }
                    await sendResponse(JsonSerializer.SerializeToElement(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = "event sent",
                        status = BuildStatus("ok", $"Event {eventType} sent to instance {targetId}")
                    }));
                    break;
                case "bpmn.getInstanceState":
                    var instanceIdState = @params.GetProperty("instanceId").GetString();
                    var stateObj = await _engine.GetInstanceStateAsync(instanceIdState!);
                    if (stateObj is not null && stateObj.GetType().GetProperty("state") != null)
                    {
                        await sendResponse(JsonSerializer.SerializeToElement(new
                        {
                            jsonrpc = "2.0",
                            id,
                            result = stateObj,
                            status = BuildStatus("ok", $"State for instance {instanceIdState}")
                        }));
                    }
                    else
                    {
                        await sendResponse(JsonSerializer.SerializeToElement(new
                        {
                            jsonrpc = "2.0",
                            id,
                            error = BuildError(-32004, "Instance not found", new { instanceId = instanceIdState })
                        }));
                    }
                    break;
                default:
                    await sendResponse(JsonSerializer.SerializeToElement(new
                    {
                        jsonrpc = "2.0",
                        id,
                        error = BuildError(-32601, "Method not found", new { method })
                    }));
                    break;
            }
        }
        catch (Exception ex)
        {
            await sendResponse(JsonSerializer.SerializeToElement(new
            {
                jsonrpc = "2.0",
                id,
                error = BuildError(-32000, ex.Message, new { exception = ex.GetType().Name })
            }));
        }
    }
}