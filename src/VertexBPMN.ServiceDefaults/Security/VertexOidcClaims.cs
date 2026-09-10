using System.Security.Claims;

namespace VertexBPMN.ServiceDefaults.Security;

/// <summary>
/// Validates and normalizes the provider-neutral OIDC claims consumed by VertexBPMN.
/// This helper must only be called after the protocol handler has validated the token.
/// </summary>
public static class VertexOidcClaims
{
    public const string RolesClaimType = "roles";
    public const string TenantClaimType = "tenant_id";
    public const string SubjectClaimType = "sub";

    private static readonly HashSet<string> AllowedRoles =
    [
        "Admin",
        "ProcessManager",
        "ReadOnly"
    ];

    public static bool TryNormalize(ClaimsPrincipal principal, out string error)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var identity = principal.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated);
        if (identity is null)
        {
            error = "The OIDC identity is not authenticated.";
            return false;
        }

        if (!HasExactlyOneNonEmptyClaim(principal, SubjectClaimType))
        {
            error = "The OIDC identity must contain exactly one non-empty subject claim.";
            return false;
        }

        if (!HasExactlyOneNonEmptyClaim(principal, TenantClaimType))
        {
            error = "The OIDC identity must contain exactly one non-empty tenant claim.";
            return false;
        }

        // Only the explicitly mapped top-level OIDC claim may grant VertexBPMN roles.
        // In particular, arbitrary realm, management or pre-mapped role claims are ignored.
        foreach (var existing in principal.FindAll(ClaimTypes.Role).ToArray())
            existing.Subject?.RemoveClaim(existing);

        var normalizedRoles = principal.FindAll(RolesClaimType)
            .Select(claim => claim.Value.Trim())
            .Where(AllowedRoles.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var role in normalizedRoles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        error = string.Empty;
        return true;
    }

    private static bool HasExactlyOneNonEmptyClaim(ClaimsPrincipal principal, string claimType)
    {
        var claims = principal.FindAll(claimType).ToArray();
        return claims.Length == 1 && !string.IsNullOrWhiteSpace(claims[0].Value);
    }
}
