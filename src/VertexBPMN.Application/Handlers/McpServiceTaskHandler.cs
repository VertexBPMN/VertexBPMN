using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain;
using VertexBPMN.Domain.Contracts;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.EngineServices.Handlers;

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
        span.SetAttribute("mcpMethod", attributes.TryGetValue("mcpMethod", out var mcpMethodAttr) ? mcpMethodAttr : "unknown");
        span.SetAttribute("mcpServerUrl", attributes.TryGetValue("mcpServerUrl", out var mcpServerUrlAttr) ? mcpServerUrlAttr : "unknown");

        try
        {
            // Erforderliche Attribute aus BPMN-Definition
            if (!attributes.TryGetValue("mcpServerUrl", out var mcpServerUrl) || string.IsNullOrEmpty(mcpServerUrl))
                throw new DistributedTokenException("MCP ServiceTask requires 'mcpServerUrl' attribute");
            if (!attributes.TryGetValue("mcpMethod", out var mcpMethod) || string.IsNullOrEmpty(mcpMethod))
                throw new DistributedTokenException("MCP ServiceTask requires 'mcpMethod' attribute");

            // Parameter aus Prozessvariablen oder Attributen
            var mcpParams = new Dictionary<string, object>();
            if (attributes.TryGetValue("mcpParams", out var paramsJson))
            {
                mcpParams = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson)
                    ?? throw new DistributedTokenException("Invalid 'mcpParams' format");
            }
            foreach (var variable in variables)
            {
                mcpParams[variable.Key] = variable.Value;
            }

            // JSON-RPC 2.0-Anfrage
            var request = new
            {
                jsonrpc = "2.0",
                method = mcpMethod,
                @params = mcpParams,
                id = Guid.NewGuid().ToString()
            };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(mcpServerUrl, content, ct);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var jsonResponse = JsonSerializer.Deserialize<JsonRpcResponse>(responseContent)
                ?? throw new DistributedTokenException("Invalid MCP response");

            if (jsonResponse.Error != null)
                throw new DistributedTokenException($"MCP error: {jsonResponse.Error.Message}");

            // Ergebnisse in Prozessvariablen speichern
            if (jsonResponse.Result != null)
            {
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString() ?? "{}");
                if (result != null)
                {
                    foreach (var kvp in result)
                    {
                        variables[kvp.Key] = kvp.Value;
                    }
                }
            }

            _logger.LogInformation("Executed MCP ServiceTask {McpMethod} on {McpServerUrl}", mcpMethod, mcpServerUrl);
            span.SetStatus(Status.Ok);
        }
        catch (Exception ex)
        {
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            _logger.LogError(ex, "Failed to execute MCP ServiceTask {McpMethod} on {McpServerUrl}",
                attributes.TryGetValue("mcpMethod", out var mcpMethodLog) ? mcpMethodLog : "unknown",
                attributes.TryGetValue("mcpServerUrl", out var mcpServerUrlLog) ? mcpServerUrlLog : "unknown");
            throw new DistributedTokenException($"Failed to execute MCP ServiceTask: {ex.Message}", ex);
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
