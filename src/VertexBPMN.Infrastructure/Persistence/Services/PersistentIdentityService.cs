using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

public sealed class PersistentIdentityService : IIdentityService
{
    private readonly BpmnDbContext _bpmnDb;
    private readonly TenantDbContext _tenantDb;

    public PersistentIdentityService(BpmnDbContext bpmnDb, TenantDbContext tenantDb)
    {
        _bpmnDb = bpmnDb;
        _tenantDb = tenantDb;
    }

    public async IAsyncEnumerable<UserInfo> ListUsersAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? tenantId = null)
    {
        var users = await _bpmnDb.Users.AsNoTracking()
            .Where(user => tenantId == null || user.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        foreach (var user in users)
            yield return new UserInfo(user.Id, user.Username, user.Email);
    }

    public async ValueTask<UserInfo?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default, string? tenantId = null)
    {
        var user = await _bpmnDb.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id && (tenantId == null || user.TenantId == tenantId), cancellationToken);
        return user is null ? null : new UserInfo(user.Id, user.Username, user.Email);
    }

    public async IAsyncEnumerable<TenantInfo> ListTenantsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tenants = await _tenantDb.Tenants.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var tenant in tenants)
            yield return new TenantInfo(tenant.Id, tenant.Name);
    }

    public ValueTask<UserInfo?> ValidateUserAsync(string username, string password, CancellationToken cancellationToken = default)
        => ValueTask.FromException<UserInfo?>(new NotSupportedException(
            "Password validation is unavailable because credentials are managed by the configured external identity provider."));

    public async ValueTask<GroupInfo?> GetGroupByIdAsync(string id, CancellationToken cancellationToken = default, string? tenantId = null)
    {
        var group = await _bpmnDb.IdentityGroups.AsNoTracking()
            .SingleOrDefaultAsync(group => group.Id == id && (tenantId == null || group.TenantId == tenantId), cancellationToken);
        return group is null ? null : new GroupInfo(group.Id, group.Name, group.Type);
    }

    public async IAsyncEnumerable<GroupInfo> ListGroupsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? tenantId = null)
    {
        var groups = await _bpmnDb.IdentityGroups.AsNoTracking()
            .Where(group => tenantId == null || group.TenantId == tenantId)
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
        foreach (var group in groups)
            yield return new GroupInfo(group.Id, group.Name, group.Type);
    }

    public async IAsyncEnumerable<AuthorizationInfo> ListAuthorizationsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? tenantId = null)
    {
        var authorizations = await _bpmnDb.IdentityAuthorizations.AsNoTracking()
            .Where(authorization => tenantId == null || authorization.TenantId == tenantId)
            .OrderBy(authorization => authorization.Resource)
            .ToListAsync(cancellationToken);
        foreach (var authorization in authorizations)
            yield return new AuthorizationInfo(
                authorization.Id,
                authorization.UserId,
                authorization.GroupId,
                authorization.Resource,
                authorization.Permissions);
    }

    public async IAsyncEnumerable<UserInfo> ListUsersByGroupAsync(
        string groupId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? tenantId = null)
    {
        var users = await (
            from membership in _bpmnDb.IdentityGroupMemberships.AsNoTracking()
            join user in _bpmnDb.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.GroupId == groupId && (tenantId == null || membership.TenantId == tenantId)
            orderby user.Username
            select user)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
            yield return new UserInfo(user.Id, user.Username, user.Email);
    }
}