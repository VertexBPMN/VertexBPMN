using System.Security.Claims;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.ServiceDefaults.Security;

namespace VertexBPMN.Api.Security;

internal static class ExternalTaskWorkerPrincipal
{
    public static bool TryCreate(
        ClaimsPrincipal principal,
        out ExternalTaskWorkerContext? worker,
        out string errorCode)
    {
        worker = null;
        errorCode = "worker_forbidden";

        if (!TryGetSingle(principal, "iss", out var issuer)
            || !TryGetSingle(principal, VertexOidcClaims.SubjectClaimType, out var subject)
            || !TryGetSingle(principal, VertexOidcClaims.TenantClaimType, out var tenantId))
            return false;

        var topics = GetExactSet(principal, VertexOidcClaims.ExternalTaskTopicClaimType);
        var profiles = GetExactSet(principal, VertexOidcClaims.ExternalTaskProfileClaimType);
        if (topics is null || topics.Count == 0 || profiles is null)
            return false;

        worker = new ExternalTaskWorkerContext(issuer, subject, tenantId, topics, profiles);
        errorCode = string.Empty;
        return true;
    }

    private static bool TryGetSingle(ClaimsPrincipal principal, string claimType, out string value)
    {
        var values = principal.FindAll(claimType).Select(claim => claim.Value).ToArray();
        if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0]))
        {
            value = string.Empty;
            return false;
        }

        value = values[0];
        return true;
    }

    private static IReadOnlySet<string>? GetExactSet(ClaimsPrincipal principal, string claimType)
    {
        var values = principal.FindAll(claimType).Select(claim => claim.Value).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Any(value => value == "*" || value.Contains(',')))
            return null;

        return values.ToHashSet(StringComparer.Ordinal);
    }
}
