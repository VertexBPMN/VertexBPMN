namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Provides identity and access management operations (users, groups, tenants).
/// </summary>
public interface IIdentityService
{
    // Vertex-kompatible User-API
    IAsyncEnumerable<UserInfo> ListUsersAsync(CancellationToken cancellationToken = default, string? tenantId = null);
    ValueTask<UserInfo?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default, string? tenantId = null);

    // Vertex-kompatible Group-API
    IAsyncEnumerable<GroupInfo> ListGroupsAsync(CancellationToken cancellationToken = default, string? tenantId = null);
    ValueTask<GroupInfo?> GetGroupByIdAsync(string id, CancellationToken cancellationToken = default, string? tenantId = null);

    // Vertex-kompatible Authorization-API
    IAsyncEnumerable<AuthorizationInfo> ListAuthorizationsAsync(CancellationToken cancellationToken = default, string? tenantId = null);

    // Vorhandene Methoden
    ValueTask<UserInfo?> ValidateUserAsync(string username, string password, CancellationToken cancellationToken = default);
    IAsyncEnumerable<UserInfo> ListUsersByGroupAsync(string groupId, CancellationToken cancellationToken = default, string? tenantId = null);
    IAsyncEnumerable<TenantInfo> ListTenantsAsync(CancellationToken cancellationToken = default);
}

public record UserInfo(string Id, string Username, string Email);
public record GroupInfo(string Id, string Name, string Type);
public record AuthorizationInfo(string Id, string UserId, string GroupId, string Resource, string Permissions);
public record TenantInfo(string Id, string Name);
