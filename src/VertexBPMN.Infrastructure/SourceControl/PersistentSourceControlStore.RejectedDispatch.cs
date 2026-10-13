using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal async Task RejectUnstartedProfiledDispatchAsync(SourceControlOperationRecord observed,
		SourceControlErrorCode reason, CancellationToken cancellationToken)
	{
		if (observed.ExecutionContractVersion is not (AcceptedGitHttpsExecution.ContractVersion or AcceptedGitSshWriteExecution.ContractVersion or AcceptedHostingExecution.ContractVersion
			or AcceptedGitSshExecution.ContractVersion) || observed.Fence != 0)
		{
			return;
		}
		var terminal = reason is SourceControlErrorCode.NotFound or SourceControlErrorCode.Forbidden
			or SourceControlErrorCode.RevisionConflict or SourceControlErrorCode.ContentUnsafe or SourceControlErrorCode.InvalidInput;
		var nextState = terminal ? SourceControlOperationState.Failed : SourceControlOperationState.Queued;
		var errorCode = terminal ? (int?)reason : observed.ErrorCode;
		var now = DateTimeOffset.UtcNow.UtcTicks;
		_ = await db.SourceControlOperations.Where(row => row.Id == observed.Id && row.TenantId == observed.TenantId
			&& row.ActorId == observed.ActorId && row.ExecutionContractVersion == observed.ExecutionContractVersion
			&& row.ProtectedExecutionTarget == observed.ProtectedExecutionTarget && row.RequestHash == observed.RequestHash
			&& row.ProtectedRequest == observed.ProtectedRequest && row.State == (int)SourceControlOperationState.Queued
			&& row.Fence == 0 && row.LeaseOwner == null && row.LeaseUntilUtcTicks == null)
			.ExecuteUpdateAsync(update => update.SetProperty(row => row.State, (int)nextState)
				.SetProperty(row => row.ErrorCode, errorCode).SetProperty(row => row.UpdatedUtcTicks, now), cancellationToken);
	}
}
