using System.Data;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal async Task<bool> FinishSshReadAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		IReadOnlyCollection<string> currentRoles, GitSshPolicyRegistry policies, T result, CancellationToken cancellationToken)
	{
		Relational();
		var roles = currentRoles.ToArray();
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await ReadClaimedSshReadAsync(context, id, worker, fence, roles, policies, cancellationToken);
		var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(candidate => candidate.TenantId == context.TenantId
			&& candidate.ActorId == context.ActorId && candidate.Id == id, cancellationToken);
		var protectedResult = new GitReadResultProtection(protection).Protect(row, result);
		var now = DateTimeOffset.UtcNow.UtcTicks;
		var changed = await db.SourceControlOperations.Where(candidate => candidate.TenantId == context.TenantId
			&& candidate.ActorId == context.ActorId && candidate.Id == id && candidate.Fence == fence
			&& candidate.LeaseOwner == worker && candidate.State == (int)SourceControlOperationState.Running
			&& candidate.LeaseUntilUtcTicks > now)
			.ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.ProtectedResult, protectedResult), cancellationToken);
		if (changed != 1 || !await FinishAsync(context.TenantId, id, worker, fence, SourceControlOperationState.Succeeded,
			DateTimeOffset.UtcNow, cancellationToken))
		{
			return false; // Disposal rolls back a result that could not be confirmed under this lease.
		}
		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	internal async Task<T> ReadSshResultAsync<T>(SourceControlContext context, Guid id, IReadOnlyCollection<string> currentRoles,
		GitSshPolicyRegistry policies, CancellationToken cancellationToken)
	{
		var (row, _) = await ReadAcceptedSshReadRecordAsync(context, id, currentRoles, policies, cancellationToken);
		return new GitReadResultProtection(protection).Unprotect<T>(row);
	}
}
