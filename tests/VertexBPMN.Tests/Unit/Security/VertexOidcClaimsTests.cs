using System.Security.Claims;
using VertexBPMN.ServiceDefaults.Security;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class VertexOidcClaimsTests
{
    [Fact]
    public void TryNormalize_MapsOnlyAllowedTopLevelRoles()
    {
        var principal = Principal(
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("roles", "Admin"),
            new Claim("roles", "ReadOnly"),
            new Claim("roles", "realm-admin"),
            new Claim("resource_access", "Admin"),
            new Claim(ClaimTypes.Role, "untrusted-pre-mapped-role"));

        var result = VertexOidcClaims.TryNormalize(principal, out var error);

        Assert.True(result, error);
        Assert.True(principal.IsInRole("Admin"));
        Assert.True(principal.IsInRole("ReadOnly"));
        Assert.False(principal.IsInRole("realm-admin"));
        Assert.False(principal.IsInRole("untrusted-pre-mapped-role"));
        Assert.Equal(2, principal.FindAll(ClaimTypes.Role).Count());
    }

    [Fact]
    public void TryNormalize_IsIdempotent()
    {
        var principal = Principal(
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("roles", "ProcessManager"));

        Assert.True(VertexOidcClaims.TryNormalize(principal, out var firstError), firstError);
        Assert.True(VertexOidcClaims.TryNormalize(principal, out var secondError), secondError);

        Assert.Single(principal.FindAll(ClaimTypes.Role));
        Assert.True(principal.IsInRole("ProcessManager"));
    }

    [Theory]
    [InlineData("sub", null)]
    [InlineData("sub", "")]
    [InlineData("sub", "duplicate")]
    [InlineData("tenant_id", null)]
    [InlineData("tenant_id", "")]
    [InlineData("tenant_id", "duplicate")]
    public void TryNormalize_RejectsMissingEmptyOrDuplicateIdentityClaims(
        string claimType,
        string? variant)
    {
        var claims = new List<Claim>
        {
            new("sub", "user-1"),
            new("tenant_id", "tenant-a"),
            new("roles", "ReadOnly")
        };

        claims.RemoveAll(claim => claim.Type == claimType);
        if (variant is not null)
            claims.Add(new Claim(claimType, variant));
        if (variant == "duplicate")
            claims.Add(new Claim(claimType, variant));

        var principal = Principal(claims.ToArray());

        Assert.False(VertexOidcClaims.TryNormalize(principal, out var error));
        Assert.Contains(claimType == "sub" ? "subject" : "tenant", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "oidc",
            nameType: "preferred_username",
            roleType: ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
