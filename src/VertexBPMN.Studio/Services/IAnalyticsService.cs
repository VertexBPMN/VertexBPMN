using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IAnalyticsService
{
    Task<JsonElement> GetEventStatsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetProcessMetricsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetEventsAsync(CancellationToken cancellationToken = default);
}
