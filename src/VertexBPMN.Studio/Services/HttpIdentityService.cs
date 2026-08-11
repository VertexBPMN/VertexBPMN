using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpIdentityService(IHttpClientFactory httpClientFactory) : IIdentityService
{
    public async Task<IReadOnlyList<StudioTenant>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        var tenants = await client.GetFromJsonAsync<List<StudioTenant>>(
            "/api/tenant",
            cancellationToken);

        return tenants ?? [];
    }

    public async Task<StudioTenant> CreateTenantAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(
            "/api/tenant",
            new { name, description },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudioTenant>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned no tenant.");
    }

    public async Task UpdateTenantAsync(
        string id,
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PutAsJsonAsync(
            $"/api/tenant/{Uri.EscapeDataString(id)}",
            new { name, description },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTenantAsync(string id, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.DeleteAsync(
            $"/api/tenant/{Uri.EscapeDataString(id)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
