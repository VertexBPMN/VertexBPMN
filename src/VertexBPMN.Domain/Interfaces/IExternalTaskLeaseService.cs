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

public sealed record ExternalTaskCompleteCommand(
    Guid LeaseId,
    long LeaseGeneration,
    Guid CompletionId,
    JsonElement Result);

public sealed record ExternalTaskFailCommand(
    Guid LeaseId,
    long LeaseGeneration,
    Guid FailureId,
    string Kind,
    string Code);

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

public sealed record ExternalTaskMutationResult(
    Guid JobId,
    string State,
    string? ContinuationState,
    long? AvailableAt,
    int AttemptsStarted);

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

public sealed record ExternalTaskRecoveryResult(int Examined, int Transitioned, int Conflicts);

public interface IExternalTaskRecoveryService
{
    ValueTask<ExternalTaskRecoveryResult> RecoverAsync(
        int maximumJobs = 100,
        CancellationToken cancellationToken = default);
}
