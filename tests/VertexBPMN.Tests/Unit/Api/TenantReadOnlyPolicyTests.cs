using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Api.Security;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class TenantReadOnlyPolicyTests
{
    [Theory]
    [InlineData("ReadOnly", null, false)]
    [InlineData("ReadOnly", "", false)]
    [InlineData("ReadOnly", " ", false)]
    [InlineData("ReadOnly", "tenant-a", true)]
    [InlineData("ProcessManager", "tenant-a", true)]
    [InlineData("Admin", null, true)]
    [InlineData("Unknown", "tenant-a", false)]
    public async Task ProductionPolicy_RequiresRoleAndTenant_ExceptForAdmin(string role, string? tenant, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(SecurityConfiguration.AddTenantReadOnlyPolicy);
        await using var provider = services.BuildServiceProvider();
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (tenant is not null) claims.Add(new Claim("tenant_id", tenant));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(user, null, "TenantReadOnly");
        Assert.Equal(allowed, result.Succeeded);
    }
}
