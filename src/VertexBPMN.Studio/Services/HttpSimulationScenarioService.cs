using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpSimulationScenarioService(IHttpClientFactory httpClientFactory) : ISimulationScenarioService
{
    public async Task<JsonElement> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var uri = string.IsNullOrWhiteSpace(tenantId)
            ? "/api/simulation-scenario"
            : $"/api/simulation-scenario?tenantId={Uri.EscapeDataString(tenantId)}";
        return await SendGetAsync(uri, cancellationToken);
    }

    public async Task<JsonElement> CreateAsync(object scenario, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync("/api/simulation-scenario", scenario, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public Task<JsonElement> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        SendGetAsync($"/api/simulation-scenario/{Uri.EscapeDataString(id)}", cancellationToken);

    public async Task<JsonElement> UpdateAsync(string id, object scenario, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PutAsJsonAsync($"/api/simulation-scenario/{Uri.EscapeDataString(id)}", scenario, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.DeleteAsync($"/api/simulation-scenario/{Uri.EscapeDataString(id)}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<JsonElement> SendGetAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
