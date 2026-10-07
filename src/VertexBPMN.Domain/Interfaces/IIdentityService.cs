namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Provides identity and access management operations (users, groups, tenants).
/// </summary>
public interface IIdentityService
{
	// Vertex-kompatible User-API
	IAsyncEnumerable<UserInfo> ListUsersAsync(string? tenantId = null, CancellationToken cancellationToken = default);
	ValueTask<UserInfo?> GetUserByIdAsync(string id, string? tenantId = null, CancellationToken cancellationToken = default);

	// Vertex-kompatible Group-API
	IAsyncEnumerable<GroupInfo> ListGroupsAsync(string? tenantId = null, CancellationToken cancellationToken = default);
	ValueTask<GroupInfo?> GetGroupByIdAsync(string id, string? tenantId = null, CancellationToken cancellationToken = default);

	// Vertex-kompatible Authorization-API
	IAsyncEnumerable<AuthorizationInfo> ListAuthorizationsAsync(string? tenantId = null, CancellationToken cancellationToken = default);

	// Vorhandene Methoden
	ValueTask<UserInfo?> ValidateUserAsync(string username, string password, CancellationToken cancellationToken = default);
	IAsyncEnumerable<UserInfo> ListUsersByGroupAsync(string groupId, string? tenantId = null, CancellationToken cancellationToken = default);
	IAsyncEnumerable<TenantInfo> ListTenantsAsync(CancellationToken cancellationToken = default);
}
