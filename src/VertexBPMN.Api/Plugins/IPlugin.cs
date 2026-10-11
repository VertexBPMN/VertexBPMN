namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public interface IPlugin
{
    Task InitializeAsync(PluginContext context);
    Task ShutdownAsync();
    Task EnableAsync();
    Task DisableAsync();
    Task RegisterServicesAsync(PluginServiceContainer serviceContainer);
    Task<object?> ExecuteMethodAsync(string methodName, params object[] parameters);
    Task<List<PluginExtensionPoint>> GetExtensionPointsAsync();
}
