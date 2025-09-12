namespace VertexBPMN.Api.Plugins;

/// <summary>
/// Plugin Architecture System
/// Olympic-level feature: Innovation Differentiators - Plugin Extensions
/// </summary>
public interface IPluginManager
{
    Task<PluginLoadResult> LoadPluginAsync(string pluginPath);
    Task<bool> UnloadPluginAsync(string pluginId);
    Task<List<PluginInfo>> GetLoadedPluginsAsync();
    Task<PluginInfo?> GetPluginInfoAsync(string pluginId);
    Task<bool> EnablePluginAsync(string pluginId);
    Task<bool> DisablePluginAsync(string pluginId);
    Task<T?> GetPluginServiceAsync<T>(string pluginId) where T : class;
    Task<PluginExecutionResult> ExecutePluginMethodAsync(string pluginId, string methodName, params object[] parameters);
    Task<List<PluginExtensionPoint>> GetAvailableExtensionPointsAsync();
    Task<bool> RegisterExtensionPointAsync(PluginExtensionPoint extensionPoint);
}