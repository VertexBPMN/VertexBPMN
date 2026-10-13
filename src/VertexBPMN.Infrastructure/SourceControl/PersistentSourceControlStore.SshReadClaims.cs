using System.Data;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal async Task<long?> TryClaimSshReadAsync(SourceControlContext context, Guid id, string worker,
		DateTimeOffset now, TimeSpan duration, IReadOnlyCollection<string> currentRoles,
		GitSshPolicyRegistry policies, CancellationToken cancellationToken)
	{
		Relational();
		ValidateHttpsReadClaim(worker, duration);
		var roles = currentRoles.ToArray();
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		try
		{
			_ = await ReadAcceptedSshReadAsync(context, id, roles, policies, cancellationToken);
			var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(candidate => candidate.TenantId == context.TenantId
				&& candidate.ActorId == context.ActorId && candidate.Id == id, cancellationToken);
			if (!HttpsReadCanBeClaimed(row, now) || !await HasHttpsReadSlotAsync(context.TenantId, now, cancellationToken))
			{
				return null;
			}
			var changed = await db.SourceControlOperations.Where(candidate => candidate.TenantId == context.TenantId
				&& candidate.ActorId == context.ActorId && candidate.Id == id && candidate.Kind == (int)SourceControlOperationKind.Read
				&& candidate.ExecutionContractVersion == AcceptedGitSshExecution.ContractVersion
				&& candidate.ProtectedExecutionTarget == row.ProtectedExecutionTarget && candidate.ProtectedRequest == row.ProtectedRequest
				&& candidate.RequestHash == row.RequestHash && candidate.Fence == row.Fence && candidate.State == row.State
				&& (!candidate.LeaseUntilUtcTicks.HasValue || candidate.LeaseUntilUtcTicks <= now.UtcTicks))
				.ExecuteUpdateAsync(update => update
					.SetProperty(candidate => candidate.State, (int)SourceControlOperationState.Running)
					.SetProperty(candidate => candidate.Fence, candidate => candidate.Fence + 1)
					.SetProperty(candidate => candidate.LeaseOwner, worker)
					.SetProperty(candidate => candidate.LeaseUntilUtcTicks, now.Add(duration).UtcTicks)
					.SetProperty(candidate => candidate.UpdatedUtcTicks, now.UtcTicks), cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return changed == 1 ? row.Fence + 1 : null;
		}
		catch (Exception error) when (IsClaimContention(error))
		{
			return null;
		}
	}

	internal async Task<AcceptedGitSshReadWork> ReadClaimedSshReadAsync(SourceControlContext context, Guid id,
		string worker, long fence, IReadOnlyCollection<string> currentRoles, GitSshPolicyRegistry policies, CancellationToken cancellationToken)
	{
		var work = await ReadAcceptedSshReadAsync(context, id, currentRoles, policies, cancellationToken);
		var now = DateTimeOffset.UtcNow.UtcTicks;
		if (!await db.SourceControlOperations.AsNoTracking().AnyAsync(row => row.TenantId == context.TenantId
			&& row.ActorId == context.ActorId && row.Id == id && row.Kind == (int)SourceControlOperationKind.Read
			&& row.ExecutionContractVersion == AcceptedGitSshExecution.ContractVersion
			&& row.State == (int)SourceControlOperationState.Running && row.LeaseOwner == worker
			&& row.Fence == fence && row.LeaseUntilUtcTicks > now, cancellationToken))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		return work;
	}
}
