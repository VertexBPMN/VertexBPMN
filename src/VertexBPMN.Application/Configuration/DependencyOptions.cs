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
