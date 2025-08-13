
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Persistence.Services;

public class IdentityService : IIdentityService
{
    public async IAsyncEnumerable<UserInfo> ListUsersAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield break;
    }
    public ValueTask<UserInfo?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default) => new((UserInfo?)null);
    public async IAsyncEnumerable<GroupInfo> ListGroupsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield break;
    }
    public ValueTask<GroupInfo?> GetGroupByIdAsync(string id, CancellationToken cancellationToken = default) => new((GroupInfo?)null);
    public async IAsyncEnumerable<AuthorizationInfo> ListAuthorizationsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield break;
    }
    public ValueTask<UserInfo?> ValidateUserAsync(string username, string password, CancellationToken cancellationToken = default) => new((UserInfo?)null);
    public async IAsyncEnumerable<UserInfo> ListUsersByGroupAsync(string groupId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield break;
    }
    public async IAsyncEnumerable<TenantInfo> ListTenantsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield break;
    }
}
