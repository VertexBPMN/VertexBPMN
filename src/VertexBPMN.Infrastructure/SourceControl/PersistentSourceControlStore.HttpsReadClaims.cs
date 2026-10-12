using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal Task<Guid> EnqueueProfiledRevisionReadAsync(SourceControlContext context, SourceControlExecutionTarget target,
		IReadOnlyCollection<string> currentRoles, SourceControlIdempotencyKey key, GitCommitId revision,
		GitHttpsPolicyRegistry policies, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(revision);
		return EnqueueProfiledReadAsync(context, target, currentRoles, key,
			JsonSerializer.SerializeToUtf8Bytes(new AcceptedGitRevisionRead(revision)), policies, cancellationToken);
	}

	internal async Task<long?> TryClaimHttpsRevisionReadAsync(SourceControlContext context, Guid id, string worker,
		DateTimeOffset now, TimeSpan duration, IReadOnlyCollection<string> currentRoles,
		GitHttpsPolicyRegistry policies, CancellationToken cancellationToken)
	{
		Relational();
		ValidateHttpsReadClaim(worker, duration);
		var roles = currentRoles.ToArray();
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		try
		{
			await ReadHttpsRevisionReadWorkAsync(context, id, roles, policies, cancellationToken);
			var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(candidate => candidate.TenantId == context.TenantId
				&& candidate.ActorId == context.ActorId && candidate.Id == id, cancellationToken);
			if (!HttpsReadCanBeClaimed(row, now) || !await HasHttpsReadSlotAsync(context.TenantId, now, cancellationToken))
			{
				return null;
			}
			var changed = await db.SourceControlOperations.Where(candidate => candidate.TenantId == context.TenantId
				&& candidate.ActorId == context.ActorId && candidate.Id == id
				&& candidate.Kind == (int)SourceControlOperationKind.Read
				&& candidate.ExecutionContractVersion == AcceptedGitHttpsExecution.ContractVersion
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

	internal async Task<AcceptedGitRevisionReadWork> ReadClaimedHttpsRevisionReadAsync(SourceControlContext context, Guid id,
		string worker, long fence, IReadOnlyCollection<string> currentRoles, GitHttpsPolicyRegistry policies,
		CancellationToken cancellationToken)
	{
		Relational();
		var roles = currentRoles.ToArray();
		var work = await ReadHttpsRevisionReadWorkAsync(context, id, roles, policies, cancellationToken);
		var now = DateTimeOffset.UtcNow.UtcTicks;
		if (!await db.SourceControlOperations.AsNoTracking().AnyAsync(row => row.TenantId == context.TenantId
			&& row.ActorId == context.ActorId && row.Id == id && row.Kind == (int)SourceControlOperationKind.Read
			&& row.ExecutionContractVersion == AcceptedGitHttpsExecution.ContractVersion
			&& row.State == (int)SourceControlOperationState.Running && row.LeaseOwner == worker
			&& row.Fence == fence && row.LeaseUntilUtcTicks > now, cancellationToken))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		return work;
	}

	private async Task<AcceptedGitRevisionReadWork> ReadHttpsRevisionReadWorkAsync(SourceControlContext context, Guid id,
		IReadOnlyCollection<string> roles, GitHttpsPolicyRegistry policies, CancellationToken cancellationToken)
	{
		var (row, execution) = await ReadAcceptedHttpsExecutionRecordAsync(context, id, roles, policies, cancellationToken);
		if (row.ProtectedRequest.Length > 6 * 1024 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		try
		{
			var request = JsonSerializer.Deserialize<AcceptedGitRevisionRead>(Unprotect(context, row.RepositoryId, row.ProtectedRequest));
			if (request is null || request.SchemaVersion != 1
				|| (request.Query is null && (request.Revision is null || row.ProtectedRequest.Length > 2048)))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			if (request.Query is { } query)
			{
				var binding = (await FindAsync(context.TenantId, row.RepositoryId, cancellationToken))?.Binding
					?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
				query.Validate(binding, request.Revision, options.Value.Limits);
			}
			return new(execution, request.Revision) { Query = request.Query };
		}
		catch (Exception error) when (error is JsonException or ArgumentException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}

	private async Task<bool> HasHttpsReadSlotAsync(string tenantId, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var active = db.SourceControlOperations.AsNoTracking().Where(row => row.LeaseUntilUtcTicks > now.UtcTicks
			&& (row.State == (int)SourceControlOperationState.Running || row.State == (int)SourceControlOperationState.Reconciling));
		return await active.CountAsync(cancellationToken) < options.Value.Limits.MaxConcurrentJobsTotal
			&& await active.CountAsync(row => row.TenantId == tenantId, cancellationToken) < options.Value.Limits.MaxConcurrentJobsPerTenant;
	}

	private static bool HttpsReadCanBeClaimed(SourceControlOperationRecord row, DateTimeOffset now) =>
		row.State is (int)SourceControlOperationState.Queued or (int)SourceControlOperationState.Running
			or (int)SourceControlOperationState.ResultUnknown or (int)SourceControlOperationState.Reconciling
		&& (!row.LeaseUntilUtcTicks.HasValue || row.LeaseUntilUtcTicks <= now.UtcTicks);

	private static void ValidateHttpsReadClaim(string worker, TimeSpan duration)
	{
		if (string.IsNullOrWhiteSpace(worker) || worker.Length > 128 || worker.Any(char.IsControl)
			|| duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(5))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
	}
}
