using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Handlers;

public class McpServiceTaskHandler : IServiceTaskHandler
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<McpServiceTaskHandler> _logger;
	private readonly Tracer _tracer;

	public McpServiceTaskHandler(HttpClient httpClient, ILogger<McpServiceTaskHandler> logger, TracerProvider tracerProvider)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tracer = tracerProvider.GetTracer("VertexBPMN");
	}

	public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
	{
		using var span = _tracer.StartActiveSpan("McpServiceTask");
		span.SetAttribute("mcpMethod", GetAttribute(attributes, "mcpMethod"));
		span.SetAttribute("mcpServerUrl", GetAttribute(attributes, "mcpServerUrl"));

		try
		{
			// Erforderliche Attribute aus BPMN-Definition
			var mcpServerUrl = GetRequiredAttribute(attributes, "mcpServerUrl", "MCP ServiceTask requires 'mcpServerUrl' attribute");
			var mcpMethod = GetRequiredAttribute(attributes, "mcpMethod", "MCP ServiceTask requires 'mcpMethod' attribute");

			var mcpParams = BuildMcpParams(attributes, variables);

			// JSON-RPC 2.0-Anfrage
			var request = new
			{
				jsonrpc = "2.0",
				method = mcpMethod,
				@params = mcpParams,
				id = Guid.NewGuid().ToString()
			};
			using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
			using var response = await _httpClient.PostAsync(mcpServerUrl, content, ct);
			response.EnsureSuccessStatusCode();

			var responseContent = await response.Content.ReadAsStringAsync(ct);
			var jsonResponse = JsonSerializer.Deserialize<JsonRpcResponse>(responseContent)
				?? throw new DistributedTokenException("Invalid MCP response");

			if (jsonResponse.Error != null)
			{
				throw new DistributedTokenException($"MCP error: {jsonResponse.Error.Message}");
			}

			// Ergebnisse in Prozessvariablen speichern
			ApplyJsonRpcResults(jsonResponse, variables);

			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Executed MCP ServiceTask {McpMethod} on {McpServerUrl}", mcpMethod, mcpServerUrl);
			}
			span.SetStatus(Status.Ok);
		}
		catch (Exception ex)
		{
			span.SetStatus(Status.Error.WithDescription(ex.Message));
			_logger.LogError(ex, "Failed to execute MCP ServiceTask {McpMethod} on {McpServerUrl}",
				GetAttribute(attributes, "mcpMethod"),
				GetAttribute(attributes, "mcpServerUrl"));
			throw new DistributedTokenException($"Failed to execute MCP ServiceTask: {ex.Message}", ex);
		}
	}

	private static string GetAttribute(IDictionary<string, string> attributes, string key) =>
		attributes.TryGetValue(key, out var value) ? value : "unknown";

	private static string GetRequiredAttribute(IDictionary<string, string> attributes, string key, string errorMessage)
	{
		if (!attributes.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
		{
			throw new DistributedTokenException(errorMessage);
		}
		return value;
	}

	private static Dictionary<string, object> BuildMcpParams(IDictionary<string, string> attributes, IDictionary<string, object> variables)
	{
		// Parameter aus Prozessvariablen oder Attributen
		var mcpParams = new Dictionary<string, object>(StringComparer.Ordinal);
		if (attributes.TryGetValue("mcpParams", out var paramsJson))
		{
			mcpParams = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson)
				?? throw new DistributedTokenException("Invalid 'mcpParams' format");
		}
		foreach (var variable in variables)
		{
			mcpParams[variable.Key] = variable.Value;
		}
		return mcpParams;
	}

	private static void ApplyJsonRpcResults(JsonRpcResponse jsonResponse, IDictionary<string, object> variables)
	{
		// Ergebnisse in Prozessvariablen speichern
		if (jsonResponse.Result == null)
		{
			return;
		}

		var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString() ?? "{}");
		if (result == null)
		{
			return;
		}

		foreach (var kvp in result)
		{
			variables[kvp.Key] = kvp.Value;
		}
	}
}

//public class McpServiceTaskHandler
//{
//    private readonly IMcpAgentService _agentService;

//    public McpServiceTaskHandler(IMcpAgentService agentService)
//    {
//        _agentService = agentService;
//    }

//    /// <summary>
//    /// Führt einen Service Task mit MCP-Agent aus und mapped Input/Output.
//    /// </summary>
//    public async Task HandleServiceTaskAsync(string agentName, JsonNode processVariables, Action<JsonNode> outputMapping, CancellationToken ct)
//    {
//        try
//        {
//            var response = await _agentService.CallAgentAsync(agentName, processVariables, ct);
//            outputMapping(response);
//        }
//        catch (Exception ex)
//        {
//            // Retry/Fehlerbehandlung kann hier ergänzt werden
//            throw new Exception($"MCP-Agent-Call fehlgeschlagen: {ex.Message}", ex);
//        }
//    }
//}
