namespace VertexBPMN.Api.Plugins;

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