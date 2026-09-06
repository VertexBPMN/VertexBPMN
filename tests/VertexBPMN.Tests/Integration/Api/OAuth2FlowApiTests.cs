using System.Net;
using System.Net.Http.Json;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

/// <summary>
/// Proves the OAuth2 authorization surface end to end against the real controller + flow service +
/// BpmnDbContext: issuing a tenant-scoped state and the anonymous callback route's invalid/expired handling.
/// The live token exchange (authorization_code / refresh_token) is covered at the unit level
/// (OAuth2CredentialFlowServiceTests) because the shared test host cannot reach a real token endpoint.
/// </summary>
[Collection("IntegratedApi")]
public sealed class OAuth2FlowApiTests
{
    private readonly HttpClient _client;

    public OAuth2FlowApiTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _client = factory.WithSharedFixture(dbFixture).CreateClient(output);
    }

    [Fact]
    public async Task Authorize_IssuesTenantScopedState_AndCallbackRejectsUnknownState()
    {
        var tenantId = $"tenant-{Guid.NewGuid():N}";

        var create = await _client.PostAsJsonAsync("/api/credentials", new
        {
            tenantId,
            name = "OAuth2 CRM",
            type = "oauth2",
            description = "CRM integration",
            secrets = new Dictionary<string, string>
            {
                ["client_id"] = "client-1",
                ["client_secret"] = "super-secret-client"
            }
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var credentialId = (await create.Content.ReadFromJsonAsync<CredentialDto>(cancellationToken: TestContext.Current.CancellationToken))!.Id;

        var authorize = await _client.PostAsJsonAsync("/api/oauth2/authorize", new
        {
            tenantId,
            credentialId,
            config = new
            {
                authorizationUrl = "https://auth.example/authorize",
                tokenUrl = "https://auth.example/token",
                clientId = "client-1",
                redirectUri = "https://app.example/callback",
                scopes = "read write"
            }
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorize.StatusCode);
        var start = await authorize.Content.ReadFromJsonAsync<OAuth2StartDto>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(start);
        Assert.False(string.IsNullOrEmpty(start!.State));
        Assert.Contains("client_id=client-1", start.RedirectUrl, StringComparison.Ordinal);
        Assert.Contains($"state={Uri.EscapeDataString(start.State)}", start.RedirectUrl, StringComparison.Ordinal);

        // The callback endpoint is anonymous; an unknown state must be rejected (401), never leak info.
        var staleCallback = await _client.GetAsync(
            $"/api/oauth2/callback?state=does-not-exist&code=abc", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, staleCallback.StatusCode);
    }

    [Fact]
    public async Task Authorize_RejectsUnknownCredential()
    {
        var tenantId = $"tenant-{Guid.NewGuid():N}";
        var authorize = await _client.PostAsJsonAsync("/api/oauth2/authorize", new
        {
            tenantId,
            credentialId = "missing-credential",
            config = new
            {
                authorizationUrl = "https://auth.example/authorize",
                tokenUrl = "https://auth.example/token",
                clientId = "client-1",
                redirectUri = "https://app.example/callback",
                scopes = "read write"
            }
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, authorize.StatusCode);
    }

    public sealed record CredentialDto(string Id, string TenantId, string Name, string Type, IReadOnlyList<string> SecretKeys);

    public sealed record OAuth2StartDto(string RedirectUrl, string State);
}
