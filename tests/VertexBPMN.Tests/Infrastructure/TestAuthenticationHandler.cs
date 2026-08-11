using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VertexBPMN.Tests.Infrastructure;

internal sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var hasExplicitUser = Request.Headers.ContainsKey("X-Test-User");
        var claims = hasExplicitUser
            ? new[]
            {
                new Claim(ClaimTypes.Name, Request.Headers["X-Test-User"].ToString()),
                new Claim(ClaimTypes.Role, "ReadOnly"),
                new Claim("tenant_id", Request.Headers["X-Test-Tenant"].FirstOrDefault() ?? "vertexbpmn")
            }
            : new[]
            {
                new Claim(ClaimTypes.Name, "test-admin"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("tenant_id", "vertexbpmn")
            };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}