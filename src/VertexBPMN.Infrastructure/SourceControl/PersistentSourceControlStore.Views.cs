using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	public async Task<IReadOnlyList<RepositoryAccessSnapshot>> ListBindingsAsync(SourceControlContext context,
		IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		Relational();
		var rows = await db.SourceControlBindings.AsNoTracking().Where(x => x.TenantId == context.TenantId)
			.OrderBy(x => x.Id).Take(1000).ToArrayAsync(cancellationToken);
		var visible = new List<RepositoryAccessSnapshot>();
		foreach (var row in rows)
		{
			var access = await FindAsync(context.TenantId, row.Id, cancellationToken);
			if (access is null) continue;
			try { RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, RepositoryPermission.Read); }
			catch (SourceControlSecurityException exception) when (exception.Code == SourceControlErrorCode.NotFound) { continue; }
			visible.Add(access);
		}
		return visible;
	}

	public async Task<CommitReceipt?> GetCommitReceiptAsync(SourceControlContext context, Guid id,
		IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		var operation = await GetOperationAsync(context, id, roles, cancellationToken);
		if (operation?.ActorId != context.ActorId || operation.Kind != SourceControlOperationKind.Commit
			|| operation.State != SourceControlOperationState.CommittedLocal) return null;
		var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(x => x.Id == id && x.TenantId == context.TenantId, cancellationToken);
		if (row.ProtectedResult is null) return null;
		try
		{
			var stored = JsonSerializer.Deserialize<StoredLocalCommit>(Unprotect(context, row.RepositoryId, row.ProtectedResult));
			if (stored?.SchemaVersion != 1 || stored.RepositoryId != row.RepositoryId || stored.Receipt.OperationId != id)
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			return stored.Receipt;
		}
		catch (JsonException) { throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
	}

	public async Task<bool> AdvanceSessionAfterPushAsync(SourceControlContext context, Guid sessionId, Guid pushId,
		long expectedRevision, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		Relational();
		var push = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pushId
			&& x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.Kind == (int)SourceControlOperationKind.Push
			&& x.State == (int)SourceControlOperationState.Pushed, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var access = await FindAsync(context.TenantId, push.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, RepositoryPermission.Commit);
		var accepted = JsonSerializer.Deserialize<AcceptedPush>(Unprotect(context, push.RepositoryId, push.ProtectedRequest))
			?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		if (accepted.SchemaVersion != 1 || accepted.Source.SessionId != sessionId || accepted.Source.ActorAuthority != options.Value.IdentityAuthority)
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		var session = await db.SourceControlSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId
			&& x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.RepositoryId == push.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		if (session.BaseCommit == accepted.Receipt.Commit.Value) return true;
		var now = DateTimeOffset.UtcNow;
		return await db.SourceControlSessions.Where(x => x.Id == sessionId && x.TenantId == context.TenantId && x.ActorId == context.ActorId
			&& x.Revision == expectedRevision && x.BaseCommit == accepted.Source.BaseCommit && x.ExpiresUtcTicks > now.UtcTicks)
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.BaseCommit, accepted.Receipt.Commit.Value)
				.SetProperty(x => x.Revision, x => x.Revision + 1)
				.SetProperty(x => x.ExpiresUtcTicks, now.Add(options.Value.Limits.SessionIdleRetention).UtcTicks), cancellationToken) == 1;
	}
}
