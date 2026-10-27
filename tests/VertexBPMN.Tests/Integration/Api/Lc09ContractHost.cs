using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Infrastructure.Operational;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Integration.Api;

// Contract host: test principal, not JWT/OIDC acceptance evidence.
internal static class Lc09ContractHost
{
    internal static string Connection => Environment.GetEnvironmentVariable("LC09_TEST_CONNECTION")
        ?? throw new InvalidOperationException("LC09_TEST_CONNECTION must point to an isolated store.");
    internal static BpmnDbContext Database() => new(new DbContextOptionsBuilder<BpmnDbContext>()
        .UseVertexNpgsql(Connection).Options);

    internal static Task<WebApplication> StartAsync(string tenant) => StartAsync(tenant, ["ReadOnly"], featureOn: true);

    internal static async Task<WebApplication> StartAsync(string? tenant, string[] roles, bool featureOn = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Runtime:EventSubscriptions:Enabled"] = featureOn ? "true" : "false"
        });
        builder.Services.AddDbContext<BpmnDbContext>(o => o.UseVertexNpgsql(Connection));
        builder.Services.AddLogging();
        builder.Services.AddSingleton<RuntimeEventSubscriptionProjectionService>();
        builder.Services.AddAuthentication("Noop")
            .AddScheme<AuthenticationSchemeOptions, NoopAuthHandler>("Noop", _ => { });
        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy("TenantReadOnly", p => p.RequireClaim("tenant_id"));
            o.AddPolicy("TenantSubscriptionManager", p => p.RequireAuthenticatedUser()
                .RequireRole("Admin", "ProcessManager").RequireClaim("tenant_id"));
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(EventSubscriptionController).Assembly);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var claims = new List<Claim>();
            if (tenant is not null)
            {
                claims.Add(new Claim("tenant_id", tenant));
            }

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ContractPrincipal"));
            await next(context);
        });
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    // Provides a default challenge/forbid scheme so policy failures yield a real 403/401
    // instead of "no scheme" (production registers a real bearer scheme).
    internal sealed class NoopAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public NoopAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}
