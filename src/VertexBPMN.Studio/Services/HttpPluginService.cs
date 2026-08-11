using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpPluginService(IHttpClientFactory httpClientFactory) : IPluginService
{
    public Task<JsonElement> GetPluginsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("/api/plugins", cancellationToken);

    public Task<JsonElement> GetExtensionPointsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("/api/plugins/extension-points", cancellationToken);

    private async Task<JsonElement> GetAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}