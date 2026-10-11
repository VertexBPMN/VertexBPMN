using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;
internal sealed class HttpFeatureFlagService(IHttpClientFactory httpClientFactory) : IFeatureFlagService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");
    public async Task<JsonElement> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<JsonElement>("api/feature-flags", cancellationToken);
    }

    public async Task SetAsync(string name, bool enabled, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"api/feature-flags/{Uri.EscapeDataString(name)}", enabled, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
