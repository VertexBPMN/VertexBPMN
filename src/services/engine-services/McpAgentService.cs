using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using System.Linq;
using System;

namespace VertexBPMN.Core.Services;
/// <summary>
/// Service für MCP-Agent-Kommunikation (REST + WebSocket)
/// </summary>
public class McpAgentService
{
    private readonly Dictionary<string, AgentConfig> _agents;
    private readonly HttpClient _httpClient;

    public McpAgentService(string configPath)
    {
        _httpClient = new HttpClient();
        var configJson = File.ReadAllText(configPath);
        var config = JsonNode.Parse(configJson)!["agents"]!.AsArray();
        _agents = config.ToDictionary(
            x => x["name"]!.ToString(),
            x => new AgentConfig
            {
                Name = x["name"]!.ToString(),
                Type = x["type"]!.ToString(),
                Url = x["url"]!.ToString(),
                Auth = x["auth"]?.ToString()
            });
    }

    /// <summary>
    /// Ruft einen MCP-Agent synchron/asynchron per REST auf.
    /// </summary>
    public async Task<JsonNode> CallAgentAsync(string agentName, JsonNode input, CancellationToken ct)
    {
        if (!_agents.TryGetValue(agentName, out var agent))
            throw new ArgumentException($"Agent '{agentName}' nicht gefunden.");
        var req = new StringContent(input.ToJsonString(), Encoding.UTF8, "application/json");
        var resp = await _httpClient.PostAsync(agent.Url, req, ct);
        resp.EnsureSuccessStatusCode();
        var respJson = await resp.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(respJson)!;
    }

    /// <summary>
    /// Wartet auf eine Antwort eines MCP-Agents (z.B. WebSocket, CorrelationId)
    /// </summary>
    public async Task<JsonNode> WaitForAgentResponseAsync(string correlationId, CancellationToken ct)
    {
        // Demo: Simuliert Antwort nach 1 Sekunde
        await Task.Delay(1000, ct);
        return new JsonObject { ["correlationId"] = correlationId, ["result"] = "DemoResponse" };
    }

    private class AgentConfig
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "REST";
        public string Url { get; set; } = "";
        public string? Auth { get; set; }
    }
}
