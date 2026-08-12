using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IPluginService
{
    Task<JsonElement> GetPluginsAsync(CancellationToken cancellationToken = default);

    Task<JsonElement> GetExtensionPointsAsync(CancellationToken cancellationToken = default);
}