using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Api.Migration;
/// <summary>
/// Erweiterung für Service Tasks: MCP-Agent-Integration
/// </summary>
public class McpServiceTaskHandler
{
    private readonly McpAgentService _agentService;

    public McpServiceTaskHandler(McpAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// Führt einen Service Task mit MCP-Agent aus und mapped Input/Output.
    /// </summary>
    public async Task HandleServiceTaskAsync(string agentName, JsonNode processVariables, Action<JsonNode> outputMapping, CancellationToken ct)
    {
        try
        {
            var response = await _agentService.CallAgentAsync(agentName, processVariables, ct);
            outputMapping(response);
        }
        catch (Exception ex)
        {
            // Retry/Fehlerbehandlung kann hier ergänzt werden
            throw new Exception($"MCP-Agent-Call fehlgeschlagen: {ex.Message}", ex);
        }
    }
}
