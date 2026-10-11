namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public class PluginDependency
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool Required { get; set; }
}
