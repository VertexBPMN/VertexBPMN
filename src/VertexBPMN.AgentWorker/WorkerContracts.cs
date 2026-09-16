using System.Text.Json;
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
    ValueTask<ExternalTaskWorkerMutationResult> CompleteAsync(
        ExternalTaskLease lease, Guid completionId, JsonElement result, CancellationToken cancellationToken);
    ValueTask<ExternalTaskWorkerMutationResult> FailAsync(
        ExternalTaskLease lease, Guid failureId, string kind, string code, CancellationToken cancellationToken);
}

public enum ExternalTaskHeartbeatOutcome { Accepted, LeaseLost }

public sealed record ExternalTaskWorkerHeartbeatResult(
    ExternalTaskHeartbeatOutcome Outcome,
    long? LeaseExpiresAt = null);

public enum ExternalTaskMutationOutcome { Accepted, LeaseLost, Rejected }

public sealed record ExternalTaskWorkerMutationResult(ExternalTaskMutationOutcome Outcome);

public enum ExternalTaskHandlerOutcome { Success, TechnicalFailure, BusinessError }

public sealed record ExternalTaskHandlerResult(
    ExternalTaskHandlerOutcome Outcome,
    JsonElement Result = default,
    string? Code = null)
{
    public static ExternalTaskHandlerResult Success(JsonElement result) =>
        new(ExternalTaskHandlerOutcome.Success, result.Clone());
    public static ExternalTaskHandlerResult TechnicalFailure(string code) =>
        new(ExternalTaskHandlerOutcome.TechnicalFailure, Code: code);
    public static ExternalTaskHandlerResult BusinessError(string code) =>
        new(ExternalTaskHandlerOutcome.BusinessError, Code: code);
}

/// <summary>
/// Performs only the long-running external work. Implementations receive no API client,
/// so the host remains the sole owner of lease mutation and completion.
/// </summary>
public interface IExternalTaskHandler
{
    string Topic { get; }
    ValueTask<ExternalTaskHandlerResult> HandleAsync(ExternalTaskLease lease, CancellationToken cancellationToken);
}

public sealed class ExternalTaskTransportException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
    : HttpRequestException(message, inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
