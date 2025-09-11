using System.Reflection;

namespace VertexBPMN.Api.Plugins;

public class LoadedPlugin
{
    public string Id { get; set; } = string.Empty;
    public PluginMetadata Metadata { get; set; } = new();
    public IPlugin Instance { get; set; } = null!;
    public Assembly Assembly { get; set; } = null!;
    public PluginAssemblyLoadContext AssemblyContext { get; set; } = null!;
    public PluginServiceContainer ServiceContainer { get; set; } = new();
    public DateTime LoadedAt { get; set; }
    public bool IsEnabled { get; set; }
    public PluginStatus Status { get; set; }
}