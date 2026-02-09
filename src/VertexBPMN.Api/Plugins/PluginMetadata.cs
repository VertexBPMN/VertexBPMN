namespace VertexBPMN.Api.Plugins;

public class PluginMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<PluginDependency> Dependencies { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}