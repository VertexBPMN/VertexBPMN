using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using VertexBPMN.Api.Security;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class JwtBearerHandlerOidcTests
{
    private const string Issuer = "https://issuer.vertexbpmn.test/realms/vertexbpmn";
    private const string Audience = "vertexbpmn-api";

    [Fact]
    public async Task ValidSignedToken_ReachesProtectedEndpointWithNormalizedClaims()
    {
        using var signingKey = RSA.Create(2048);
        await using var host = await CreateHostAsync(signingKey);
        var token = Token(signingKey, Issuer, Audience, DateTime.UtcNow.AddMinutes(5), includeSubject: true, includeTenant: true);

        var response = await SendAsync(host, token);

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected a valid signed token to be accepted, but received {response.StatusCode}: " +
            string.Join("; ", response.Headers.TryGetValues("X-Test-Auth-Failure", out var errors) ? errors : []));
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("user-1|tenant-a|ProcessManager", body);
    }

    [Theory]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("expired")]
    [InlineData("wrong-signature")]
    [InlineData("missing-subject")]
    [InlineData("missing-tenant")]
    public async Task InvalidToken_IsRejectedByRealJwtBearerHandler(string variant)
    {
        using var trustedKey = RSA.Create(2048);
        using var untrustedKey = RSA.Create(2048);
        await using var host = await CreateHostAsync(trustedKey);
        var token = Token(
            variant == "wrong-signature" ? untrustedKey : trustedKey,
            variant == "wrong-issuer" ? "https://attacker.invalid" : Issuer,
            variant == "wrong-audience" ? "different-api" : Audience,
            variant == "expired" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(5),
            includeSubject: variant != "missing-subject",
            includeTenant: variant != "missing-tenant");

        var response = await SendAsync(host, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<WebApplication> CreateHostAsync(RSA trustedKey)
    {
        var securityKey = new RsaSecurityKey(trustedKey) { KeyId = "trusted-key" };
        var oidcConfiguration = new OpenIdConnectConfiguration { Issuer = Issuer };
        oidcConfiguration.SigningKeys.Add(securityKey);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProductionSecurity(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OperationalMode"] = "Production",
                ["Jwt:Authority"] = Issuer,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:RequireHttpsMetadata"] = "true"
            })
            .Build());
        builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>>(
            new StaticJwtConfiguration(oidcConfiguration));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/secured", (HttpContext context) => string.Join('|',
                context.User.FindFirst("sub")?.Value,
                context.User.FindFirst("tenant_id")?.Value,
                context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value))
            .RequireAuthorization();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<HttpResponseMessage> SendAsync(WebApplication app, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/secured");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string Token(
        RSA key,
        string issuer,
        string audience,
        DateTime expires,
        bool includeSubject,
        bool includeTenant)
    {
        var claims = new Dictionary<string, object>
        {
            ["preferred_username"] = "vertex-user",
            ["roles"] = "ProcessManager"
        };
        if (includeSubject)
            claims["sub"] = "user-1";
        if (includeTenant)
            claims["tenant_id"] = "tenant-a";

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            NotBefore = expires.AddMinutes(-5),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(key) { KeyId = "trusted-key" },
                SecurityAlgorithms.RsaSha256)
        });
    }

    private sealed class StaticJwtConfiguration(OpenIdConnectConfiguration configuration)
        : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (name == JwtBearerDefaults.AuthenticationScheme)
            {
                options.Configuration = configuration;
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.Events.OnAuthenticationFailed = context =>
                {
                    context.Response.Headers["X-Test-Auth-Failure"] =
                        $"{context.Exception.GetType().Name}: {context.Exception.Message}";
                    return Task.CompletedTask;
                };
            }
        }
    }
}
