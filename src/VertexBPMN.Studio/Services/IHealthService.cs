using System.Text.Json;

namespace VertexBPMN.Studio.Services;

internal interface IHealthService
{
    Task<JsonElement> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetComprehensiveHealthAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetSystemMetricsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetCircuitBreakersAsync(CancellationToken cancellationToken = default);
}
