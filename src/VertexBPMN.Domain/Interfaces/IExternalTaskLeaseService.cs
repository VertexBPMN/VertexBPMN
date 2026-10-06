using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public interface IExternalTaskLeaseService
{
	ValueTask<IReadOnlyList<ExternalTaskLease>> ClaimAsync(
		ExternalTaskWorkerContext worker,
		ExternalTaskClaimCommand command,
		CancellationToken cancellationToken = default);

	ValueTask<ExternalTaskHeartbeatResult> HeartbeatAsync(
		ExternalTaskWorkerContext worker,
		Guid jobId,
		ExternalTaskHeartbeatCommand command,
		CancellationToken cancellationToken = default);

	ValueTask<ExternalTaskMutationResult> CompleteAsync(
		ExternalTaskWorkerContext worker,
		Guid jobId,
		ExternalTaskCompleteCommand command,
		CancellationToken cancellationToken = default);

	ValueTask<ExternalTaskMutationResult> FailAsync(
		ExternalTaskWorkerContext worker,
		Guid jobId,
		ExternalTaskFailCommand command,
		CancellationToken cancellationToken = default);

	ValueTask<ExternalTaskStatus> GetAsync(
		ExternalTaskWorkerContext worker,
		Guid jobId,
		CancellationToken cancellationToken = default);

	ValueTask<ExternalTaskAttemptPage> GetAttemptsAsync(
		ExternalTaskWorkerContext worker,
		Guid jobId,
		string? after,
		int limit,
		CancellationToken cancellationToken = default);
}
