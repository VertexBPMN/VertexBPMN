using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpCredentialService(IHttpClientFactory httpClientFactory) : ICredentialService
{
    public async Task<IReadOnlyList<StudioCredential>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        return await client.GetFromJsonAsync<List<StudioCredential>>($"/api/credentials?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken) ?? [];
    }

    public async Task<StudioCredential> CreateAsync(string tenantId, string name, string type, string? description, string key, string value, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync("/api/credentials", new { tenantId, name, type, description, secrets = new Dictionary<string, string> { [key] = value } }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudioCredential>(cancellationToken) ?? throw new InvalidOperationException("The API returned no credential metadata.");
    }

    public async Task UpdateMetadataAsync(string tenantId, string id, string name, string type, string? description, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PutAsJsonAsync($"/api/credentials/{Uri.EscapeDataString(id)}", new { tenantId, name, type, description }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RotateSecretAsync(string tenantId, string id, string key, string value, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PutAsJsonAsync($"/api/credentials/{Uri.EscapeDataString(id)}/secret", new { tenantId, key, value }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.DeleteAsync($"/api/credentials/{Uri.EscapeDataString(id)}?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> StartOAuth2AuthorizationAsync(string tenantId, string credentialId, OAuth2ConnectConfig config, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync("/api/oauth2/authorize", new
        {
            tenantId,
            credentialId,
            config = new { config.AuthorizationUrl, config.TokenUrl, config.ClientId, config.RedirectUri, config.Scopes }
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var start = await response.Content.ReadFromJsonAsync<OAuth2AuthorizationStartDto>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned no OAuth2 authorization start.");
        return start.RedirectUrl;
    }

    private sealed record OAuth2AuthorizationStartDto(string RedirectUrl, string State);
}
