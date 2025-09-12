namespace VertexBPMN.Api.Plugins;

public class PluginDependency
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool Required { get; set; }
}