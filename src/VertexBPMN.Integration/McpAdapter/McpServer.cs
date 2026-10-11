using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.McpAdapter;

internal sealed class McpServer
{

	private static JsonElement BuildError(int code, string message, object? data = null)
	{
		var errorObj = new
		{
			code,
			message,
			data
		};
		return JsonSerializer.SerializeToElement(errorObj);
	}

	private static JsonElement BuildStatus(string status, string? detail = null, object? data = null)
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
	private readonly ConcurrentDictionary<string, Channel<JsonElement>> _eventChannels = new(StringComparer.Ordinal);

	public McpServer(IBpmnEngine engine, ILogger<McpServer> logger)
	{
		_engine = engine;
		_logger = logger;
	}

	public ChannelReader<JsonElement>? GetInstanceEventStream(string instanceId)
	{
		if (_eventChannels.TryGetValue(instanceId, out var channel))
		{
			return channel.Reader;
		}

		return null;
	}

	public async Task HandleJsonRpcAsync(string json, Func<JsonElement, Task> sendResponse)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var id = root.TryGetProperty("id", out var idProp) ? idProp : default;
		var method = root.GetProperty("method").GetString();
		var @params = root.TryGetProperty("params", out var p) ? p : default;

		try
		{
			switch (method)
			{
				case "bpmn.listProcesses":
					await ListProcessesAsync(id, sendResponse);
					break;
				case "bpmn.startInstance":
					await StartInstanceAsync(id, @params, sendResponse);
					break;
				case "bpmn.instanceEvent":
					await SubscribeInstanceAsync(id, @params, sendResponse);
					break;
				case "bpmn.sendInstanceEvent":
					await SendInstanceEventAsync(id, @params, sendResponse);
					break;
				case "bpmn.getInstanceState":
					await GetInstanceStateAsync(id, @params, sendResponse);
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
			_logger.LogError(ex, "MCP method {Method} failed", method);
			await sendResponse(JsonSerializer.SerializeToElement(new
			{
				jsonrpc = "2.0",
				id,
				error = BuildError(-32000, ex.Message, new { exception = ex.GetType().Name })
			}));
		}
	}
	private async Task ListProcessesAsync(JsonElement id, Func<JsonElement, Task> sendResponse)
	{
		var processes = await _engine.ListProcessesAsync();
		await sendResponse(JsonSerializer.SerializeToElement(new
		{
			jsonrpc = "2.0",
			id,
			result = processes,
			status = BuildStatus("ok", "Process list fetched")
		}));
	}

	private async Task StartInstanceAsync(JsonElement id, JsonElement parameters, Func<JsonElement, Task> sendResponse)
	{
		var key = parameters.GetProperty("processKey").GetString();
		var variables = parameters.GetProperty("variables").Deserialize<Dictionary<string, object>>();
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
	}

	private async Task SubscribeInstanceAsync(JsonElement id, JsonElement parameters, Func<JsonElement, Task> sendResponse)
	{
		// Event streaming: subscribe client to instance event channel
		var instId = parameters.GetProperty("instanceId").GetString();
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
	}

	private async Task SendInstanceEventAsync(JsonElement id, JsonElement parameters, Func<JsonElement, Task> sendResponse)
	{
		// For testing: send a custom event to the instance channel
		var targetId = parameters.GetProperty("instanceId").GetString();
		var eventType = parameters.GetProperty("type").GetString();
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
	}

	private async Task GetInstanceStateAsync(JsonElement id, JsonElement parameters, Func<JsonElement, Task> sendResponse)
	{
		var instanceIdState = parameters.GetProperty("instanceId").GetString();
		var stateObj = await _engine.GetInstanceStateAsync(instanceIdState!);
		if (stateObj?.GetType().GetProperty("state") is not null)
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
	}
}
