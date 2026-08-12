namespace VertexBPMN.Api.Plugins;

public class PluginContext
{
    public string PluginId { get; set; } = string.Empty;
    public IServiceProvider ServiceProvider { get; set; } = null!;
    public IConfiguration Configuration { get; set; } = null!;
    public ILogger Logger { get; set; } = null!;
    public List<PluginExtensionPoint> ExtensionPoints { get; set; } = new();
}