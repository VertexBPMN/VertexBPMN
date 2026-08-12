using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IPluginService
{
    Task<JsonElement> GetPluginsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetExtensionPointsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> LoadAsync(string pluginPath, CancellationToken cancellationToken = default);
    Task UnloadAsync(string pluginId, CancellationToken cancellationToken = default);
    Task EnableAsync(string pluginId, CancellationToken cancellationToken = default);
    Task DisableAsync(string pluginId, CancellationToken cancellationToken = default);
}
