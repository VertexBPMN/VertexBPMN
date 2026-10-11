namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public class PluginLoadResult
{
    public bool Success { get; set; }
    public string? PluginId { get; set; }
    public string? Error { get; set; }
    public PluginInfo? PluginInfo { get; set; }
}
