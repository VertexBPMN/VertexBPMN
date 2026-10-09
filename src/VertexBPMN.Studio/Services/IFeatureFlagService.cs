using System.Text.Json;

namespace VertexBPMN.Studio.Services;

internal interface IFeatureFlagService
{
    Task<JsonElement> GetAllAsync(CancellationToken cancellationToken = default);

    Task SetAsync(string name, bool enabled, CancellationToken cancellationToken = default);
}
