namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public class PluginContext
{
    public string PluginId { get; set; } = string.Empty;
    public IServiceProvider ServiceProvider { get; set; } = null!;
    public IConfiguration Configuration { get; set; } = null!;
    public ILogger Logger { get; set; } = null!;
    public List<PluginExtensionPoint> ExtensionPoints { get; set; } = new();
}
