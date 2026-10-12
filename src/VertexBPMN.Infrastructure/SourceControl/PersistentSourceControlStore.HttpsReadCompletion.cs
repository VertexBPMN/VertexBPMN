using System.Data;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal async Task<bool> FinishHttpsRevisionReadAsync(SourceControlContext context, Guid id, string worker, long fence,
		IReadOnlyCollection<string> currentRoles, GitHttpsPolicyRegistry policies, CancellationToken cancellationToken)
	{
		Relational();
		var roles = currentRoles.ToArray();
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await ReadClaimedHttpsRevisionReadAsync(context, id, worker, fence, roles, policies, cancellationToken);
		var completed = await FinishAsync(context.TenantId, id, worker, fence, SourceControlOperationState.Succeeded,
			DateTimeOffset.UtcNow, cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return completed;
	}
}
