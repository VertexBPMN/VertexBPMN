using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskWorkerContext(
    string Issuer,
    string Subject,
    string TenantId,
    IReadOnlySet<string> Topics,
    IReadOnlySet<string> Profiles);

public sealed record ExternalTaskClaimCommand(
    IReadOnlyCollection<string> Topics,
    int MaxTasks = 1,
    int LeaseSeconds = 60);

public sealed record ExternalTaskHeartbeatCommand(
    Guid LeaseId,
    long LeaseGeneration,
    int LeaseSeconds = 60);

public sealed record ExternalTaskLease(
    Guid JobId,
    Guid ActivityExecutionId,
    string Topic,
    string ContractVersion,
    string? AgentProfileVersion,
    JsonElement Input,
    string SchemaSnapshot,
    Guid LeaseId,
    long LeaseGeneration,
    long LeaseExpiresAt,
    long Deadline,
    int AttemptNumber,
    int MaxAttempts);

public sealed record ExternalTaskHeartbeatResult(
    Guid JobId,
    long LeaseGeneration,
    long LeaseExpiresAt,
    long ServerTime);

public sealed record ExternalTaskStatus(
    Guid JobId,
    Guid ActivityExecutionId,
    string Topic,
    string State,
    string ContractVersion,
    string? AgentProfileVersion,
    long Deadline,
    int AttemptsStarted,
    int MaxAttempts,
    long LeaseGeneration,
    long? LeaseExpiresAt);

public sealed record ExternalTaskAttemptStatus(
    Guid AttemptId,
    int AttemptNumber,
    long LeaseGeneration,
    long StartedAt,
    long? EndedAt,
    string? EndReason,
    string? ErrorCode);

public sealed record ExternalTaskAttemptPage(
    IReadOnlyList<ExternalTaskAttemptStatus> Items,
    string? NextCursor);

public sealed class ExternalTaskLeaseException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}

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
