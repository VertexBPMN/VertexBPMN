using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpAnalyticsService(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpAnalyticsService> logger) : IAnalyticsService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");

    public Task<JsonElement> GetEventStatsAsync(CancellationToken cancellationToken = default)
        => GetAsync("api/analytics/event-stats", cancellationToken);

    public Task<JsonElement> GetProcessMetricsAsync(CancellationToken cancellationToken = default)
        => GetAsync("api/analytics/metrics/process", cancellationToken);

    public Task<JsonElement> GetEventsAsync(CancellationToken cancellationToken = default)
        => GetAsync("api/analytics/events", cancellationToken);

    private async Task<JsonElement> GetAsync(string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            response.EnsureSuccessStatusCode();
            return payload;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Analytics request failed for {Endpoint}", endpoint);
            throw;
        }
    }
}
