using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface ISimulationScenarioService
{
    Task<JsonElement> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<JsonElement> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<JsonElement> CreateAsync(object scenario, CancellationToken cancellationToken = default);
    Task<JsonElement> UpdateAsync(string id, object scenario, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
