using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpFeatureFlagService(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpFeatureFlagService> logger) : IFeatureFlagService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");

    public async Task<JsonElement> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<JsonElement>("api/feature-flags", cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Feature flags could not be loaded");
            throw;
        }
    }

    public async Task SetAsync(string name, bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PutAsJsonAsync(
                $"api/feature-flags/{Uri.EscapeDataString(name)}",
                enabled,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Feature flag {FlagName} could not be updated", name);
            throw;
        }
    }
}
