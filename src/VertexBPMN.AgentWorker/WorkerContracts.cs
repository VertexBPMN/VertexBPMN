using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

public interface IWorkerAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public interface IExternalTaskApiClient
{
    ValueTask<IReadOnlyList<ExternalTaskLease>> ClaimAsync(
        IReadOnlyCollection<string> topics, int maxTasks, int leaseSeconds, CancellationToken cancellationToken);
    ValueTask<ExternalTaskWorkerHeartbeatResult> HeartbeatAsync(
        ExternalTaskLease lease, int leaseSeconds, CancellationToken cancellationToken);
}

public enum ExternalTaskHeartbeatOutcome { Accepted, LeaseLost }

public sealed record ExternalTaskWorkerHeartbeatResult(
    ExternalTaskHeartbeatOutcome Outcome,
    long? LeaseExpiresAt = null);

/// <summary>
/// Performs only the long-running external work. Implementations receive no API client,
/// so the host remains the sole owner of lease mutation and, in A04, completion.
/// </summary>
public interface IExternalTaskHandler
{
    string Topic { get; }
    ValueTask HandleAsync(ExternalTaskLease lease, CancellationToken cancellationToken);
}

public sealed class ExternalTaskTransportException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
    : HttpRequestException(message, inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
