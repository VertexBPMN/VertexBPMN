using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IFeatureFlagService
{
    Task<JsonElement> GetAllAsync(CancellationToken cancellationToken = default);

    Task SetAsync(string name, bool enabled, CancellationToken cancellationToken = default);
}
