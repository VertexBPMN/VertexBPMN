namespace VertexBPMN.Api.Plugins;

public class PluginLoadResult
{
    public bool Success { get; set; }
    public string? PluginId { get; set; }
    public string? Error { get; set; }
    public PluginInfo? PluginInfo { get; set; }
}