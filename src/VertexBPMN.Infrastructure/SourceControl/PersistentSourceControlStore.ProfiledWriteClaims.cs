using System.Data;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal async Task<long?> TryClaimProfiledWriteAsync(SourceControlContext context, Guid id, string worker, DateTimeOffset now,
		TimeSpan duration, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		Relational();
		ValidateHttpsReadClaim(worker, duration);
		var currentRoles = roles.ToArray();
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		try
		{
			var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == id
				&& candidate.TenantId == context.TenantId && candidate.ActorId == context.ActorId, cancellationToken);
			if (row is null || row.Kind is not ((int)SourceControlOperationKind.Commit or (int)SourceControlOperationKind.Push)
				|| row.ExecutionContractVersion != AcceptedGitHttpsExecution.ContractVersion)
			{
				return null;
			}
			await DemandWriteExecutionAsync(context, row, currentRoles, cancellationToken);
			if (!HttpsReadCanBeClaimed(row, now) || !await HasHttpsReadSlotAsync(context.TenantId, now, cancellationToken))
			{
				return null;
			}
			var changed = await ClaimProfiledWriteRowAsync(row, worker, now, duration, cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return changed == 1 ? row.Fence + 1 : null;
		}
		catch (Exception error) when (IsClaimContention(error))
		{
			return null;
		}
	}

	private Task<int> ClaimProfiledWriteRowAsync(SourceControlOperationRecord row, string worker, DateTimeOffset now,
		TimeSpan duration, CancellationToken cancellationToken)
	{
		var state = row.State == (int)SourceControlOperationState.Queued
			? SourceControlOperationState.Running : SourceControlOperationState.Reconciling;
		return db.SourceControlOperations.Where(candidate => candidate.Id == row.Id && candidate.TenantId == row.TenantId
			&& candidate.ActorId == row.ActorId && candidate.Kind == row.Kind
			&& candidate.ExecutionContractVersion == row.ExecutionContractVersion
			&& candidate.ProtectedExecutionTarget == row.ProtectedExecutionTarget && candidate.ProtectedRequest == row.ProtectedRequest
			&& candidate.RequestHash == row.RequestHash && candidate.State == row.State && candidate.Fence == row.Fence
			&& (!candidate.LeaseUntilUtcTicks.HasValue || candidate.LeaseUntilUtcTicks <= now.UtcTicks))
			.ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.State, (int)state)
				.SetProperty(candidate => candidate.Fence, candidate => candidate.Fence + 1)
				.SetProperty(candidate => candidate.LeaseOwner, worker)
				.SetProperty(candidate => candidate.LeaseUntilUtcTicks, now.Add(duration).UtcTicks)
				.SetProperty(candidate => candidate.UpdatedUtcTicks, now.UtcTicks), cancellationToken);
	}
}
