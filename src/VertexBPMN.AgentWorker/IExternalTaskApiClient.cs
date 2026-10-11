using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

internal interface IExternalTaskApiClient
{
    ValueTask<IReadOnlyList<ExternalTaskLease>> ClaimAsync(
        IReadOnlyCollection<string> topics, int maxTasks, int leaseSeconds, CancellationToken cancellationToken);
    ValueTask<ExternalTaskWorkerHeartbeatResult> HeartbeatAsync(
        ExternalTaskLease lease, int leaseSeconds, CancellationToken cancellationToken);
    ValueTask<ExternalTaskWorkerMutationResult> CompleteAsync(
        ExternalTaskLease lease, Guid completionId, JsonElement result, CancellationToken cancellationToken);
    ValueTask<ExternalTaskWorkerMutationResult> FailAsync(
        ExternalTaskLease lease, Guid failureId, string kind, string code, CancellationToken cancellationToken);
}
