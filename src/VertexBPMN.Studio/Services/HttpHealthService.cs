using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpHealthService(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpHealthService> logger) : IHealthService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");

    public Task<JsonElement> GetHealthAsync(CancellationToken cancellationToken = default)
        => GetAsync("api/health", cancellationToken);

    public Task<JsonElement> GetComprehensiveHealthAsync(CancellationToken cancellationToken = default)
        => GetAsync("api/health/comprehensive", cancellationToken);

    public Task<JsonElement> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
        => GetAsync("api/health/metrics", cancellationToken);

    public Task<JsonElement> GetCircuitBreakersAsync(CancellationToken cancellationToken = default)
        => GetAsync("api/health/circuit-breakers", cancellationToken);

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
            logger.LogError(exception, "Health request failed for {Endpoint}", endpoint);
            throw;
        }
    }
}
