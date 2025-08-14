using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Text.Json;
using VertexBPMN.Api.Plugins;

namespace VertexBPMN.Mcp;
/// <summary>
/// BPMN-Plugin für MCP-Agent-Integration (Service Task mit implementation="mcp-agent")
/// </summary>
public class McpAgentPlugin : IPlugin
{
    private Dictionary<string, AgentConfig> _agents = new();
    private HttpClient _httpClient = new();
    private int _maxRetries = 3;
    private PluginContext? _pluginContext;
    private bool _enabled = true;

    public McpAgentPlugin() { }


    // IPlugin: Initialisierung
    public async Task InitializeAsync(PluginContext context)
    {
        _pluginContext = context;
        _httpClient = new HttpClient();
        _maxRetries = 3;
        // Konfiguration laden
        if (context.Configuration["agentsConfigPath"] is string configPath && File.Exists(configPath))
        {
            var configJson = await File.ReadAllTextAsync(configPath);
            var config = JsonNode.Parse(configJson)!["agents"]!.AsArray();
            _agents = new();
            foreach (var x in config)
            {
                _agents[x["name"]!.ToString()] = new AgentConfig
                {
                    Name = x["name"]!.ToString(),
                    Type = x["type"]!.ToString(),
                    Url = x["url"]!.ToString(),
                    Auth = x["auth"]?.ToString()
                };
            }
        }
        // Extension Point Registrierung (Service Task Handler)
        // ...optional: weitere Initialisierung...
        await Task.CompletedTask;
    }

    public async Task ShutdownAsync()
    {
        _enabled = false;
        await Task.CompletedTask;
    }

    public async Task EnableAsync()
    {
        _enabled = true;
        await Task.CompletedTask;
    }

    public async Task DisableAsync()
    {
        _enabled = false;
        await Task.CompletedTask;
    }

    public async Task RegisterServicesAsync(PluginServiceContainer serviceContainer)
    {
        // Beispiel: Service für andere Plugins bereitstellen
        serviceContainer.RegisterService<McpAgentPlugin>(this);
        await Task.CompletedTask;
    }

    public async Task<object?> ExecuteMethodAsync(string methodName, params object[] parameters)
    {
        // Ermöglicht dynamische Methodenaufrufe
        return methodName switch
        {
            "CallAgentAsync" => parameters.Length == 2 && parameters[0] is AgentConfig agent && parameters[1] is JsonNode input
                ? await CallAgentAsync(agent, input, CancellationToken.None) : null,
            _ => null
        };
    }

    public async Task<List<PluginExtensionPoint>> GetExtensionPointsAsync()
    {
        // Extension Point für Service Task Handler
        var ext = new PluginExtensionPoint
        {
            Id = "mcp-agent-service-task",
            Name = "MCP-Agent Service Task Handler",
            Description = "Service Task Handler für MCP-Agent Integration",
            InterfaceType = typeof(ICustomActivityBehavior).FullName!,
            Parameters = new Dictionary<string, PluginParameter>
            {
                { "agentName", new PluginParameter { Name = "agentName", Type = "string", Required = true } }
            }
        };
        return await Task.FromResult(new List<PluginExtensionPoint> { ext });
    }

    // Optional: Handler für Service Tasks (kann über Extension Point angebunden werden)
    public async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        if (!_enabled)
            return new ActivityExecutionResult { Success = false, Error = "Plugin disabled" };
        var agentName = context.Configuration.TryGetValue("agentName", out var nameObj) ? nameObj?.ToString() : null;
        if (agentName == null || !_agents.TryGetValue(agentName, out var agent))
        {
            return new ActivityExecutionResult { Success = false, Error = $"MCP-Agent '{agentName}' nicht gefunden." };
        }
        var input = JsonNode.Parse(JsonSerializer.Serialize(context.Variables))!;
        int attempt = 0;
        Exception? lastEx = null;
        while (attempt < _maxRetries)
        {
            try
            {
                var response = await CallAgentAsync(agent, input, CancellationToken.None);
                // Korrigiere Typkonvertierung: JsonNode zu Dictionary<string, object>
                var outputVars = new Dictionary<string, object>();
                foreach (var kv in response.AsObject())
                {
                    outputVars[kv.Key] = kv.Value is JsonValue v ? v.GetValue<object>() : kv.Value?.ToJsonString();
                }
                return new ActivityExecutionResult { Success = true, OutputVariables = outputVars };
            }
            catch (Exception ex)
            {
                lastEx = ex;
                attempt++;
                await Task.Delay(500);
            }
        }
        return new ActivityExecutionResult { Success = false, Error = $"MCP-Agent-Call fehlgeschlagen: {lastEx?.Message}" };
    }

    public async Task<JsonNode> CallAgentAsync(AgentConfig agent, JsonNode input, CancellationToken ct)
    {
        var req = new StringContent(input.ToJsonString(), Encoding.UTF8, "application/json");
        var resp = await _httpClient.PostAsync(agent.Url, req, ct);
        resp.EnsureSuccessStatusCode();
        var respJson = await resp.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(respJson)!;
    }

    // Optional: Demo-Response
    public async Task<JsonNode> WaitForAgentResponseAsync(string correlationId, CancellationToken ct)
    {
        await Task.Delay(1000, ct);
        return new JsonObject { ["correlationId"] = correlationId, ["result"] = "DemoResponse" };
    }

    public class AgentConfig
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "REST";
        public string Url { get; set; } = "";
        public string? Auth { get; set; }
    }
}
