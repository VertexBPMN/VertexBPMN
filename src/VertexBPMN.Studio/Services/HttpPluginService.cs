using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpPluginService(IHttpClientFactory httpClientFactory) : IPluginService
{
    public Task<JsonElement> GetPluginsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("/api/plugins", cancellationToken);

    public Task<JsonElement> GetExtensionPointsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("/api/plugins/extension-points", cancellationToken);

    public Task<JsonElement> LoadAsync(string pluginPath, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>("/api/plugins/load", new { pluginPath }, cancellationToken);

    public Task UnloadAsync(string pluginId, CancellationToken cancellationToken = default) =>
        PostWithoutContentAsync($"/api/plugins/unload/{Uri.EscapeDataString(pluginId)}", cancellationToken);

    public Task EnableAsync(string pluginId, CancellationToken cancellationToken = default) =>
        PostWithoutContentAsync($"/api/plugins/enable/{Uri.EscapeDataString(pluginId)}", cancellationToken);

    public Task DisableAsync(string pluginId, CancellationToken cancellationToken = default) =>
        PostWithoutContentAsync($"/api/plugins/disable/{Uri.EscapeDataString(pluginId)}", cancellationToken);

    private async Task<JsonElement> GetAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private async Task<T> PostAsync<T>(string uri, object body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(uri, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned no response.");
    }

    private async Task PostWithoutContentAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsync(uri, null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
