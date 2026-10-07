using System.Collections.Concurrent;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>
/// In-memory implementation of IIdentityService for development and testing.
/// </summary>
public class IdentityService : IIdentityService
{
	private readonly ConcurrentBag<UserInfo> _users = new()
	{
		new UserInfo("1", "admin", "admin@example.com"),
		new UserInfo("2", "user1", "user1@example.com"),
		new UserInfo("3", "user2", "user2@example.com")
	};
	private readonly ConcurrentBag<TenantInfo> _tenants = new() { new TenantInfo("1", "default") };

	public async IAsyncEnumerable<UserInfo> ListUsersAsync(string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var user in _users)
		{
			yield return user;
		}

		await Task.CompletedTask;
	}

	public ValueTask<UserInfo?> GetUserByIdAsync(string id, string? tenantId = null, CancellationToken cancellationToken = default)
		=> new((UserInfo?)null);

	public async IAsyncEnumerable<GroupInfo> ListGroupsAsync(string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		yield break;
	}

	public ValueTask<GroupInfo?> GetGroupByIdAsync(string id, string? tenantId = null, CancellationToken cancellationToken = default)
		=> new((GroupInfo?)null);

	public async IAsyncEnumerable<AuthorizationInfo> ListAuthorizationsAsync(string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		yield break;
	}

	public ValueTask<UserInfo?> ValidateUserAsync(string username, string password, CancellationToken cancellationToken = default)
	{
		// Password is ignored for in-memory stub
		var user = System.Linq.Enumerable.FirstOrDefault(_users, u => u.Username == username);
		return ValueTask.FromResult<UserInfo?>(user);
	}

	public async IAsyncEnumerable<UserInfo> ListUsersByGroupAsync(string groupId, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		// In-memory stub: return all users for any group
		foreach (var user in _users)
		{
			yield return user;
		}

		await Task.CompletedTask;
	}

	public async IAsyncEnumerable<TenantInfo> ListTenantsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var tenant in _tenants)
		{
			yield return tenant;
		}

		await Task.CompletedTask;
	}
}
