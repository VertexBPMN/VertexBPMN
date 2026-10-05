using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>
/// Evaluates trusted host identity against freshly loaded repository grants.
/// This is not an authentication handler: roles and grants must never come from request DTOs.
/// </summary>
public static class RepositoryAccessPolicy
{
    private const RepositoryPermission Known = RepositoryPermission.Read | RepositoryPermission.Commit
        | RepositoryPermission.Push | RepositoryPermission.PullRequest | RepositoryPermission.Deploy
        | RepositoryPermission.Manage;

    public static void Demand(SourceControlContext context, RepositoryBinding binding,
        IReadOnlyCollection<string> authenticatedRoles, IReadOnlyCollection<RepositoryGrant> currentGrants,
        RepositoryPermission requested)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(authenticatedRoles);
        ArgumentNullException.ThrowIfNull(currentGrants);
        if (!string.Equals(context.TenantId, binding.TenantId, StringComparison.Ordinal))
            throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        if (requested == RepositoryPermission.None || (requested & ~Known) != 0)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);

        var roleLimit = RepositoryPermission.None;
        if (authenticatedRoles.Contains("Admin", StringComparer.Ordinal)) roleLimit = Known;
        else if (authenticatedRoles.Contains("ProcessManager", StringComparer.Ordinal))
            roleLimit = Known & ~RepositoryPermission.Manage;
        else if (authenticatedRoles.Contains("ReadOnly", StringComparer.Ordinal)) roleLimit = RepositoryPermission.Read;

        var grant = currentGrants.Where(g => string.Equals(g.ActorId, context.ActorId, StringComparison.Ordinal))
            .Aggregate(RepositoryPermission.None, (permissions, g) => permissions | (g.Permissions & Known));
        // Even Manage/Deploy requires explicit Read. Admin is not an ACL bypass.
        if ((roleLimit & grant & RepositoryPermission.Read) == 0)
            throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        var required = requested | RepositoryPermission.Read;
        if ((roleLimit & grant & required) != required)
            throw new SourceControlSecurityException(SourceControlErrorCode.Forbidden);
    }

    public static void DemandSessionOwner(SourceControlContext context, RepositoryBinding binding,
        EditSession session, DateTimeOffset now)
    {
        if (context.TenantId != binding.TenantId || session.TenantId != context.TenantId
            || session.RepositoryId != binding.Id || session.ActorId != context.ActorId)
            throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        if (session.ExpiresAt <= now)
            throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
    }
}
