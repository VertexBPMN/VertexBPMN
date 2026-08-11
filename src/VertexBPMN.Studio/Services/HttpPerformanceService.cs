using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpPerformanceService(IHttpClientFactory httpClientFactory) : IPerformanceService
{
    public Task<JsonElement> GetMetricsAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync("/api/performance/metrics", cancellationToken);

    public Task<JsonElement> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync("/api/performance/dashboard", cancellationToken);

    public Task<JsonElement> GetTrendsAsync(int hours = 24, CancellationToken cancellationToken = default) =>
        GetJsonAsync($"/api/performance/trends?hours={hours}", cancellationToken);

    public Task<JsonElement> GetLoadBalancerStatusAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync("/api/performance/load-balancer", cancellationToken);

    private async Task<JsonElement> GetJsonAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        return await client.GetFromJsonAsync<JsonElement>(uri, cancellationToken);
    }
}
