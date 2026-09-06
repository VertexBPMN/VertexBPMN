using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class OAuth2CredentialFlowServiceTests
{
    private const string AccessToken = "abc123-refreshed";
    private const string RefreshToken = "refresh-xyz";

    [Fact]
    public async Task StartAuthorization_CreatesTenantScopedStateAndRedirect()
    {
        var db = NewDb();
        var cred = await CredentialAsync(db, "tenant-a");
        var flow = CreateFlow(db, new ScriptedHandler(_ => TokenResponse("authorization_code")));

        var start = await flow.StartAuthorizationAsync(
            "tenant-a", cred, new OAuth2AuthorizationConfig(
                "https://auth.example/authorize", "https://auth.example/token",
                "client-1", "https://app.example/callback", "read write"), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(start.State));
        Assert.Contains("response_type=code", start.RedirectUrl, StringComparison.Ordinal);
        Assert.Contains("client_id=client-1", start.RedirectUrl, StringComparison.Ordinal);
        Assert.Contains($"state={Uri.EscapeDataString(start.State)}", start.RedirectUrl, StringComparison.Ordinal);

        var record = await db.OAuth2FlowStates.SingleAsync(s => s.State == start.State, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("tenant-a", record.TenantId);
        Assert.Equal(cred, record.CredentialId);
        Assert.True(record.ExpiresAt > DateTime.UtcNow.AddMinutes(9));
        Assert.True(record.ExpiresAt <= DateTime.UtcNow.AddMinutes(11));
    }

    [Fact]
    public async Task CompleteAuthorization_StoresTokensAndRemovesState()
    {
        var db = NewDb();
        var cred = await CredentialAsync(db, "tenant-a");
        var handler = new ScriptedHandler(_ => TokenResponse("authorization_code"));
        var flow = CreateFlow(db, handler);

        var start = await flow.StartAuthorizationAsync(
            "tenant-a", cred, new OAuth2AuthorizationConfig(
                "https://auth.example/authorize", "https://auth.example/token",
                "client-1", "https://app.example/callback", "read write"), TestContext.Current.CancellationToken);

        var completed = await flow.CompleteAuthorizationAsync(start.State, "auth-code", TestContext.Current.CancellationToken);
        Assert.True(completed);
        Assert.Equal(1, handler.CallCount);
        Assert.Null(await db.OAuth2FlowStates.FindAsync(new object[] { start.State }, TestContext.Current.CancellationToken));

        Assert.Equal(AccessToken, await SecretAsync(db, "tenant-a", cred, "access_token"));
        Assert.Equal(RefreshToken, await SecretAsync(db, "tenant-a", cred, "refresh_token"));
        Assert.Equal("https://auth.example/token", await SecretAsync(db, "tenant-a", cred, "token_url"));
        Assert.Equal("client-1", await SecretAsync(db, "tenant-a", cred, "client_id"));
    }

    [Fact]
    public async Task CompleteAuthorization_RejectsUnknownOrExpiredState()
    {
        var db = NewDb();
        var cred = await CredentialAsync(db, "tenant-a");
        var flow = CreateFlow(db, new ScriptedHandler(_ => TokenResponse("authorization_code")));

        Assert.False(await flow.CompleteAuthorizationAsync("does-not-exist", "code", TestContext.Current.CancellationToken));

        // Expired state must be rejected and pruned.
        var start = await flow.StartAuthorizationAsync(
            "tenant-a", cred, new OAuth2AuthorizationConfig(
                "https://auth.example/authorize", "https://auth.example/token",
                "client-1", "https://app.example/callback", "read write"), TestContext.Current.CancellationToken);
        var record = await db.OAuth2FlowStates.SingleAsync(s => s.State == start.State, cancellationToken: TestContext.Current.CancellationToken);
        record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(await flow.CompleteAuthorizationAsync(start.State, "code", TestContext.Current.CancellationToken));
        Assert.Null(await db.OAuth2FlowStates.FindAsync(new object[] { start.State }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveValidAccessToken_ReturnsFreshTokenWithoutRefresh()
    {
        var db = NewDb();
        var cred = await CredentialAsync(db, "tenant-a");
        var handler = new ScriptedHandler(_ => TokenResponse("refresh_token"));
        var flow = CreateFlow(db, handler);

        await RotateAsync(db, "tenant-a", cred, "access_token", "still-fresh", "access_token");
        await RotateAsync(db, "tenant-a", cred, "expires_at", DateTime.UtcNow.AddMinutes(30).ToString("o", CultureInfo.InvariantCulture), "expires_at");

        Assert.Equal("still-fresh", await flow.ResolveValidAccessTokenAsync("tenant-a", cred, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ResolveValidAccessToken_RefreshesWhenExpired()
    {
        var db = NewDb();
        var cred = await CredentialAsync(db, "tenant-a");
        var handler = new ScriptedHandler(_ => TokenResponse("refresh_token"));
        var flow = CreateFlow(db, handler);

        await RotateAsync(db, "tenant-a", cred, "access_token", "expired-token", "access_token");
        await RotateAsync(db, "tenant-a", cred, "expires_at", DateTime.UtcNow.AddMinutes(-5).ToString("o", CultureInfo.InvariantCulture), "expires_at");
        await RotateAsync(db, "tenant-a", cred, "refresh_token", RefreshToken, "refresh_token");
        await RotateAsync(db, "tenant-a", cred, "token_url", "https://auth.example/token", "token_url");

        var token = await flow.ResolveValidAccessTokenAsync("tenant-a", cred, TestContext.Current.CancellationToken);
        Assert.Equal(AccessToken, token);
        Assert.Equal(1, handler.CallCount);

        var newExpires = await SecretAsync(db, "tenant-a", cred, "expires_at");
        Assert.True(DateTime.TryParse(newExpires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed));
        Assert.True(parsed > DateTime.UtcNow);
    }

    [Fact]
    public async Task ResolveValidAccessToken_ReturnsNull_WhenNoRefreshPossible()
    {
        var db = NewDb();
        var cred = await CredentialAsync(db, "tenant-a");
        var flow = CreateFlow(db, new ScriptedHandler(_ => TokenResponse("refresh_token")));

        Assert.Null(await flow.ResolveValidAccessTokenAsync("tenant-a", cred, TestContext.Current.CancellationToken));
    }

    private static BpmnDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new BpmnDbContext(options);
        return db;
    }

    private static async Task<string> CredentialAsync(BpmnDbContext db, string tenant)
    {
        var credService = new PersistentCredentialService(
            db, DataProtectionProvider.Create("VertexBPMN.Tests"),
            Mock.Of<IAuditLogService>(), Mock.Of<ILogger<PersistentCredentialService>>());
        var meta = await credService.CreateAsync(tenant, new CredentialWriteRequest(
            "OAuth2 cred", "oauth2", null,
            new Dictionary<string, string>
            {
                ["client_id"] = "client-1",
                ["client_secret"] = "super-secret-client"
            }), TestContext.Current.CancellationToken);
        return meta.Id;
    }

    private static async Task<string?> SecretAsync(BpmnDbContext db, string tenant, string cred, string key)
    {
        var credService = new PersistentCredentialService(
            db, DataProtectionProvider.Create("VertexBPMN.Tests"),
            Mock.Of<IAuditLogService>(), Mock.Of<ILogger<PersistentCredentialService>>());
        return await credService.ResolveSecretAsync(tenant, cred, key, TestContext.Current.CancellationToken);
    }

    private static async Task RotateAsync(BpmnDbContext db, string tenant, string cred, string key, string value, string _unused)
    {
        var credService = new PersistentCredentialService(
            db, DataProtectionProvider.Create("VertexBPMN.Tests"),
            Mock.Of<IAuditLogService>(), Mock.Of<ILogger<PersistentCredentialService>>());
        await credService.RotateSecretAsync(tenant, cred, new CredentialSecretRotation(key, value), TestContext.Current.CancellationToken);
    }

    private static OAuth2CredentialFlowService CreateFlow(BpmnDbContext db, ScriptedHandler handler)
    {
        var client = new HttpClient(handler);
        var factory = new FakeClientFactory(client);
        return new OAuth2CredentialFlowService(
            db,
            new PersistentCredentialService(db, DataProtectionProvider.Create("VertexBPMN.Tests"),
                Mock.Of<IAuditLogService>(), Mock.Of<ILogger<PersistentCredentialService>>()),
            factory, Mock.Of<IAuditLogService>(), Mock.Of<ILogger<OAuth2CredentialFlowService>>());
    }

    private static HttpResponseMessage TokenResponse(string grantType) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            access_token = AccessToken,
            refresh_token = RefreshToken,
            expires_in = 3600,
            token_type = "Bearer"
        }), Encoding.UTF8, "application/json")
    };

    private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class FakeClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
