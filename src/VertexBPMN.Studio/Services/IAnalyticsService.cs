using System.Text.Json;

namespace VertexBPMN.Studio.Services;

internal interface IAnalyticsService
{
    Task<JsonElement> GetEventStatsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetProcessMetricsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetEventsAsync(CancellationToken cancellationToken = default);
}
