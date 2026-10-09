using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;
internal sealed class HttpAnalyticsService(IHttpClientFactory httpClientFactory) : IAnalyticsService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");
    public Task<JsonElement> GetEventStatsAsync(CancellationToken cancellationToken = default) => GetAsync("api/analytics/event-stats", cancellationToken);
    public Task<JsonElement> GetProcessMetricsAsync(CancellationToken cancellationToken = default) => GetAsync("api/analytics/metrics/process", cancellationToken);
    public Task<JsonElement> GetEventsAsync(CancellationToken cancellationToken = default) => GetAsync("api/analytics/events", cancellationToken);
    private async Task<JsonElement> GetAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        response.EnsureSuccessStatusCode();
        return payload;
    }
}
