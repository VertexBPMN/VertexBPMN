using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IPerformanceService
{
    Task<JsonElement> GetMetricsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetTrendsAsync(int hours = 24, CancellationToken cancellationToken = default);
    Task<JsonElement> GetLoadBalancerStatusAsync(CancellationToken cancellationToken = default);
}
