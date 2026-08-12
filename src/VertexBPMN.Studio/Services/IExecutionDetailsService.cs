using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IExecutionDetailsService
{
    Task<JsonElement> GetJobsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetIncidentsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetVariablesAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
}
