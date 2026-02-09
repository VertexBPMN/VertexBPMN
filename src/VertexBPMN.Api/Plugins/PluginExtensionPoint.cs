namespace VertexBPMN.Api.Plugins;

public class PluginExtensionPoint
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = string.Empty;
    public string? ProviderId { get; set; }
    public Dictionary<string, PluginParameter> Parameters { get; set; } = new();
}