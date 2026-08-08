namespace VertexBPMN.Application.Configuration;

/// <summary>
/// Runtime dependency configuration bound from the "Dependencies" section.
/// Secrets remain in environment variables and are never stored here.
/// </summary>
public sealed class DependencyOptions
{
    public AiDependencyOptions Ai { get; set; } = new();
    public ServiceTaskDependencyOptions ServiceTasks { get; set; } = new();
    public InterfaceDependencyOptions Interfaces { get; set; } = new();
    public McpDependencyOptions Mcp { get; set; } = new();
    public PluginDependencyOptions Plugins { get; set; } = new();
}

public sealed class AiDependencyOptions
{
    public bool Enabled { get; set; } = true;
    public string DefaultProvider { get; set; } = "openai";
    public string DefaultModel { get; set; } = "gpt-4";
    public Dictionary<string, AiModelOptions> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AiModelOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? ApiKeyEnvironmentVariable { get; set; }
}

public sealed class ServiceTaskDependencyOptions
{
    public bool Enabled { get; set; } = true;
    public List<string> Disabled { get; set; } = [];
    public Dictionary<string, string> Mappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class InterfaceDependencyOptions
{
    public bool AiDecisionService { get; set; } = true;
    public bool McpAgentService { get; set; } = true;
    public bool LoadBalancing { get; set; } = true;
}

public sealed class McpDependencyOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class PluginDependencyOptions
{
    public bool Enabled { get; set; } = true;
    public string Directory { get; set; } = "plugins";
    public List<string> Files { get; set; } = [];
}