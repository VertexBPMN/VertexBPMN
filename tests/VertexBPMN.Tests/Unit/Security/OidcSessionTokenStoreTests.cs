using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class OidcSessionTokenStoreTests
{
    private const string Issuer = "https://issuer.vertexbpmn.test/realms/vertexbpmn";
    private const string ApiAudience = "vertexbpmn-api";
    private const string StudioClient = "vertexbpmn-studio";

    [Fact]
    public async Task ParallelRequestsForOneSession_PerformExactlyOneRefresh()
    {
        using var signingKey = RSA.Create(2048);
        var refreshCalls = 0;
        using var storeContext = CreateStore(signingKey, async request =>
        {
            Interlocked.Increment(ref refreshCalls);
            await Task.Delay(50, TestContext.Current.CancellationToken);
            return RefreshResponse(Token(signingKey, "user-1", "tenant-a"));
        });
        var principal = Principal("session-a", "user-1", "tenant-a");
        var properties = ExpiredProperties("old-access", "refresh-a");
        storeContext.Store.Register(principal, properties);

        var results = await Task.WhenAll(
            storeContext.Store.GetAccessTokenAsync(principal, null, TestContext.Current.CancellationToken),
            storeContext.Store.GetAccessTokenAsync(principal, null, TestContext.Current.CancellationToken));

        Assert.Equal(1, refreshCalls);
        Assert.Equal(results[0], results[1]);
        Assert.NotEqual("old-access", results[0]);
    }

    [Fact]
    public async Task ConcurrentSessions_NeverShareTokensOrRefreshState()
    {
        using var signingKey = RSA.Create(2048);
        var refreshCalls = 0;
        using var storeContext = CreateStore(signingKey, async request =>
        {
            Interlocked.Increment(ref refreshCalls);
            var form = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var subject = form.Contains("refresh-a", StringComparison.Ordinal) ? "user-1" : "user-2";
            var tenant = subject == "user-1" ? "tenant-a" : "tenant-b";
            return RefreshResponse(Token(signingKey, subject, tenant));
        });
        var first = Principal("session-a", "user-1", "tenant-a");
        var second = Principal("session-b", "user-2", "tenant-b");
        storeContext.Store.Register(first, ExpiredProperties("old-a", "refresh-a"));
        storeContext.Store.Register(second, ExpiredProperties("old-b", "refresh-b"));

        var tokens = await Task.WhenAll(
            storeContext.Store.GetAccessTokenAsync(first, null, TestContext.Current.CancellationToken),
            storeContext.Store.GetAccessTokenAsync(second, null, TestContext.Current.CancellationToken));

        Assert.Equal(2, refreshCalls);
        Assert.NotEqual(tokens[0], tokens[1]);
        Assert.Equal("tenant-a", storeContext.Store.GetCurrentPrincipal(first).FindFirstValue("tenant_id"));
        Assert.Equal("tenant-b", storeContext.Store.GetCurrentPrincipal(second).FindFirstValue("tenant_id"));
    }

    [Fact]
    public async Task InvalidRefreshedSignature_FailsClosedAndInvalidatesSession()
    {
        using var trustedKey = RSA.Create(2048);
        using var attackerKey = RSA.Create(2048);
        using var storeContext = CreateStore(trustedKey,
            _ => Task.FromResult(RefreshResponse(Token(attackerKey, "user-1", "tenant-a"))));
        var principal = Principal("session-a", "user-1", "tenant-a");
        storeContext.Store.Register(principal, ExpiredProperties("old-access", "refresh-a"));

        await Assert.ThrowsAsync<OidcSessionExpiredException>(() =>
            storeContext.Store.GetAccessTokenAsync(principal, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OidcSessionExpiredException>(() =>
            storeContext.Store.GetAccessTokenAsync(principal, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refresh_UpdatesTicketAndPreservesOnlyTheLocalSessionIdentity()
    {
        using var signingKey = RSA.Create(2048);
        using var storeContext = CreateStore(signingKey,
            _ => Task.FromResult(RefreshResponse(Token(signingKey, "user-1", "tenant-new"))));
        var principal = Principal("session-a", "user-1", "tenant-old");
        var properties = ExpiredProperties("old-access", "refresh-a");
        storeContext.Store.Register(principal, properties);

        var renewed = await storeContext.Store.SynchronizeTicketAsync(
            principal,
            properties,
            TestContext.Current.CancellationToken);
        var current = storeContext.Store.GetCurrentPrincipal(principal);

        Assert.True(renewed);
        Assert.NotEqual("old-access", properties.GetTokenValue("access_token"));
        Assert.Equal("session-a", current.FindFirstValue(OidcSessionTokenStore.SessionIdClaim));
        Assert.Equal("tenant-new", current.FindFirstValue("tenant_id"));
        Assert.DoesNotContain(current.Claims, claim => claim.Value == "tenant-old");
    }

    private static StoreContext CreateStore(
        RSA signingKey,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
    {
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = Issuer,
            TokenEndpoint = "https://issuer.vertexbpmn.test/token"
        };
        configuration.SigningKeys.Add(new RsaSecurityKey(signingKey) { KeyId = "trusted-key" });
        var options = new OpenIdConnectOptions
        {
            ClientId = StudioClient,
            ClientSecret = "test-secret",
            Configuration = configuration,
            Backchannel = new HttpClient(new DelegateHandler(responseFactory)),
            MapInboundClaims = false,
            TokenHandler = new JsonWebTokenHandler()
        };
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
        var store = new OidcSessionTokenStore(
            new StaticOptionsMonitor<OpenIdConnectOptions>(options),
            TimeProvider.System,
            NullLogger<OidcSessionTokenStore>.Instance);
        return new StoreContext(store, options.Backchannel);
    }

    private static ClaimsPrincipal Principal(string sessionId, string subject, string tenant)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim("tenant_id", tenant),
            new Claim("preferred_username", subject),
            new Claim("roles", "ProcessManager"),
            new Claim(OidcSessionTokenStore.SessionIdClaim, sessionId)
        ], "oidc");
        return new ClaimsPrincipal(identity);
    }

    private static AuthenticationProperties ExpiredProperties(string accessToken, string refreshToken)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O") }
        ]);
        return properties;
    }

    private static string Token(RSA key, string subject, string tenant) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = ApiAudience,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject,
                ["tenant_id"] = tenant,
                ["preferred_username"] = subject,
                ["roles"] = "ProcessManager"
            },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(key) { KeyId = "trusted-key" },
                SecurityAlgorithms.RsaSha256)
        });

    private static HttpResponseMessage RefreshResponse(string accessToken) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(new { access_token = accessToken, refresh_token = "rotated-refresh", expires_in = 300 }),
            Encoding.UTF8,
            "application/json")
    };

    private sealed class DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed record StoreContext(OidcSessionTokenStore Store, HttpClient Backchannel) : IDisposable
    {
        public void Dispose() => Backchannel.Dispose();
    }
}
