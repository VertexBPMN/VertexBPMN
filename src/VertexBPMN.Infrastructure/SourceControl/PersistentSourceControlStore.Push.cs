using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	/// <summary>
	/// Accept a separate push of an owned completed commit, copying immutable reconstruction data.
	/// </summary>
	public async Task<Guid> EnqueuePushAsync(SourceControlContext context, Guid repositoryId,
		IReadOnlyCollection<string> roles, SourceControlIdempotencyKey key, PushJobSubmission submission,
		CancellationToken cancellationToken)
	{
		Relational();
		if (submission is null || submission.CommitOperationId == Guid.Empty || submission.ExpectedRemote is null
			|| submission.ExpectedRemote.Commit?.Value.All(character => character == '0') == true)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		var copiedRoles = roles.ToArray();
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, copiedRoles, access.Grants, RepositoryPermission.Push);
		var source = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submission.CommitOperationId
			&& x.RepositoryId == repositoryId && x.TenantId == context.TenantId && x.ActorId == context.ActorId
			&& x.Kind == (int)SourceControlOperationKind.Commit && x.State == (int)SourceControlOperationState.CommittedLocal,
			cancellationToken) ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var work = DecodeCommitWork(context, source, access);
		StoredLocalCommit saved;
		AcceptedCommit accepted;
		try
		{
			saved = JsonSerializer.Deserialize<StoredLocalCommit>(Unprotect(context, repositoryId, source.ProtectedResult ?? ""))!;
			accepted = JsonSerializer.Deserialize<AcceptedCommit>(Unprotect(context, repositoryId, source.ProtectedRequest))!;
			if (saved is null || saved.SchemaVersion != 1 || saved.RepositoryId != repositoryId
				|| saved.WorkspaceFence <= 0 || saved.WorkspaceFence > source.Fence || saved.Receipt is null
				|| saved.Receipt.Commit is null || saved.Receipt.OperationId != source.Id
				|| saved.Receipt.SessionId != work.Command.SessionId || saved.Receipt.WorkBranch != work.Command.WorkBranch
				|| saved.Receipt.Snapshots is null
				|| !JsonSerializer.SerializeToUtf8Bytes(saved.Receipt.Snapshots).AsSpan().SequenceEqual(
					JsonSerializer.SerializeToUtf8Bytes(work.Command.Snapshots.Select(x =>
						new CommittedSnapshot(x.Path, x.DocumentGeneration, x.LocalRevision, x.ContentSha256)).ToArray())))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
		catch (SourceControlSecurityException) { throw; }
		catch (Exception exception) when (exception is JsonException or ArgumentException or NullReferenceException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var request = new AcceptedPush(1, access.Revision, source.Id, source.IdempotencyKey, source.RequestHash, accepted,
			saved.Receipt, submission.ExpectedRemote.Commit?.Value);
		var id = await EnqueueAsync(context, repositoryId, copiedRoles, SourceControlOperationKind.Push, key,
			JsonSerializer.SerializeToUtf8Bytes(request), cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return id;
	}

	internal async Task<AcceptedPushWork> ReadPushWorkAsync(SourceControlContext context, Guid id, string worker,
		long fence, IReadOnlyCollection<string> currentRoles, CancellationToken cancellationToken)
	{
		Relational();
		var now = DateTimeOffset.UtcNow.UtcTicks;
		var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
			&& x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.LeaseOwner == worker
			&& x.Fence == fence && x.LeaseUntilUtcTicks > now && x.Kind == (int)SourceControlOperationKind.Push
			&& (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling),
			cancellationToken) ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, currentRoles, access.Grants, RepositoryPermission.Push);
		try
		{
			var bytes = Unprotect(context, row.RepositoryId, row.ProtectedRequest);
			var accepted = JsonSerializer.Deserialize<AcceptedPush>(bytes);
			if (accepted is null || accepted.SchemaVersion != 1 || accepted.CommitOperationId == Guid.Empty
				|| accepted.Source is null || accepted.Receipt is null || accepted.Receipt.Commit is null
				|| accepted.Receipt.OperationId != accepted.CommitOperationId
				|| accepted.Receipt.SessionId != accepted.Source.SessionId || accepted.Receipt.WorkBranch != accepted.Source.WorkBranch)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			if (accepted.BindingRevision != access.Revision || RequestHash(access.Revision, bytes) != row.RequestHash)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
			}
			var sourceBytes = JsonSerializer.SerializeToUtf8Bytes(accepted.Source);
			var local = DecodeCommitWork(context, new SourceControlOperationRecord
			{
				Id = accepted.CommitOperationId,
				RepositoryId = row.RepositoryId,
				IdempotencyKey = accepted.CommitKey,
				ProtectedRequest = Protect(context, row.RepositoryId, sourceBytes),
				RequestHash = accepted.SourceRequestHash,
				State = (int)SourceControlOperationState.CommittedLocal
			}, access);
			return new(access.Binding, access.Revision,
				new(id, new(row.IdempotencyKey), accepted.Receipt.WorkBranch, accepted.Receipt.Commit,
					accepted.ExpectedCommit is null ? ExpectedRemoteRef.Absent : ExpectedRemoteRef.At(new(accepted.ExpectedCommit))),
				local, (SourceControlOperationState)row.State);
		}
		catch (SourceControlSecurityException) { throw; }
		catch (Exception exception) when (exception is JsonException or ArgumentException or NullReferenceException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}

	internal async Task PublishRemotePushAsync(SourceControlContext context, AcceptedPushWork work,
		string worker, long fence, Func<CancellationToken, Task> publish, CancellationToken cancellationToken)
	{
		Relational();
		// Keep authority and claim locks across the single bounded remote effect and durable finish.
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		if (await db.SourceControlBindings.Where(x => x.Id == work.Binding.Id && x.TenantId == context.TenantId
			&& x.Revision == work.BindingRevision).ExecuteUpdateAsync(u => u.SetProperty(x => x.Revision, x => x.Revision), cancellationToken) != 1)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
		var now = DateTimeOffset.UtcNow.UtcTicks;
		if (await db.SourceControlOperations.Where(x => x.Id == work.Command.OperationId && x.TenantId == context.TenantId
			&& x.ActorId == context.ActorId && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now
			&& x.ProtectedResult != null && x.Kind == (int)SourceControlOperationKind.Push
			&& (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling))
			.ExecuteUpdateAsync(u => u.SetProperty(x => x.Fence, x => x.Fence), cancellationToken) != 1)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		var leaseUntil = await db.SourceControlOperations.Where(x => x.Id == work.Command.OperationId)
			.Select(x => x.LeaseUntilUtcTicks).SingleAsync(cancellationToken);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var remaining = new DateTimeOffset(leaseUntil!.Value, TimeSpan.Zero) - DateTimeOffset.UtcNow;
		if (remaining <= TimeSpan.Zero)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
		}
		deadline.CancelAfter(remaining);
		await publish(deadline.Token);
		if (!await FinishAsync(context.TenantId, work.Command.OperationId, worker, fence,
			SourceControlOperationState.Pushed, DateTimeOffset.UtcNow, deadline.Token))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
		}
		await transaction.CommitAsync(deadline.Token);
	}

	internal async Task<bool> ConfirmCompletedPushAsync(SourceControlContext context, Guid id, long fence,
		PushReceipt receipt, CancellationToken cancellationToken)
	{
		var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
			&& x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.Fence == fence
			&& x.Kind == (int)SourceControlOperationKind.Push && x.State == (int)SourceControlOperationState.Pushed, cancellationToken);
		if (row?.ProtectedResult is null)
		{
			return false;
		}
		var saved = JsonSerializer.Deserialize<StoredRemotePush>(Unprotect(context, row.RepositoryId, row.ProtectedResult));
		return saved is not null && saved.SchemaVersion == 1 && saved.RepositoryId == row.RepositoryId && saved.Receipt == receipt;
	}

	private sealed record AcceptedPush(int SchemaVersion, long BindingRevision, Guid CommitOperationId,
		string CommitKey, string SourceRequestHash, AcceptedCommit Source, CommitReceipt Receipt, string? ExpectedCommit);
}
