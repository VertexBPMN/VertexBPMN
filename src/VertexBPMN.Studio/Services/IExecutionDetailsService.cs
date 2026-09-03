using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IExecutionDetailsService
{
    Task<JsonElement> GetJobsAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<JsonElement> GetIncidentsAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<JsonElement> GetVariablesAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
}
