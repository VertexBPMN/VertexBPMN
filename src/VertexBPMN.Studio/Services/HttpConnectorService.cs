using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpConnectorService(IHttpClientFactory httpClientFactory) : IConnectorService
{
    public async Task<IReadOnlyList<StudioConnector>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        return await client.GetFromJsonAsync<List<StudioConnector>>($"/api/connectors?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken) ?? [];
    }

    public async Task<StudioConnector> CreateAsync(string tenantId, string name, string type, string? description, string? endpoint, string? credentialId, bool enabled = true, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync("/api/connectors",
            new { tenantId, name, type, description, endpoint, credentialId, enabled }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudioConnector>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned no connector metadata.");
    }

    public async Task UpdateAsync(string tenantId, string id, string name, string type, string? description, string? endpoint, string? credentialId, bool enabled, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PutAsJsonAsync($"/api/connectors/{Uri.EscapeDataString(id)}",
            new { tenantId, name, type, description, endpoint, credentialId, enabled }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetEnabledAsync(string tenantId, string id, bool enabled, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PutAsJsonAsync($"/api/connectors/{Uri.EscapeDataString(id)}/enabled",
            new { tenantId, enabled }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.DeleteAsync($"/api/connectors/{Uri.EscapeDataString(id)}?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
