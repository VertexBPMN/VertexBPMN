using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VertexBPMN.Tests.Infrastructure;

/// <summary>Test identity handler supplying the portal BFF's derived claims (sub, tenant, role).</summary>
internal sealed class PortalTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationSchemeName = "PortalTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sub = Request.Headers.TryGetValue("X-Test-User", out var userHeader) && !string.IsNullOrWhiteSpace(userHeader.ToString())
            ? userHeader.ToString()
            : "u-fachnutzer";
        var tenantId = Request.Headers.TryGetValue("X-Test-Tenant", out var tenantHeader) && !string.IsNullOrWhiteSpace(tenantHeader.ToString())
            ? tenantHeader.ToString()
            : "vertexbpmn";
        var claims = new[]
        {
            new Claim("sub", sub),
            new Claim(ClaimTypes.Name, sub),
            new Claim(ClaimTypes.Role, "ReadOnly"),
            new Claim("tenant_id", tenantId),
        };
        var identity = new ClaimsIdentity(claims, AuthenticationSchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthenticationSchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
