namespace VertexBPMN.Api.Plugins;

public class PluginParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public string? Description { get; set; }
}