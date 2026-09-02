namespace VertexBPMN.Api.Plugins;

public class PluginInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public PluginStatus Status { get; set; }
    public DateTime LoadedAt { get; set; }
    public List<PluginDependency> Dependencies { get; set; } = new();
    public List<PluginExtensionPoint> ExtensionPoints { get; set; } = new();
}