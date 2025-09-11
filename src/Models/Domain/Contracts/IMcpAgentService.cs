using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace VertexBPMN.Domain.Contracts;

public interface IMcpAgentService
{
    /// <summary>
    /// Ruft einen MCP-Agent synchron/asynchron per REST auf.
    /// </summary>
    Task<JsonNode> CallAgentAsync(string agentName, JsonNode input, CancellationToken ct);

    /// <summary>
    /// Wartet auf eine Antwort eines MCP-Agents (z.B. WebSocket, CorrelationId)
    /// </summary>
    Task<JsonNode> WaitForAgentResponseAsync(string correlationId, CancellationToken ct);
}