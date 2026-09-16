namespace VertexBPMN.Studio.Services;

public interface IExternalTaskOperationsService
{
    Task<IReadOnlyList<StudioExternalTaskProfile>> ListProfilesAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudioExternalTaskOperation>> ListForProcessAsync(
        string tenantId, Guid processInstanceId, string? activityId = null, CancellationToken cancellationToken = default);
}

public sealed record StudioExternalTaskField(string Name, string Type, bool Required, int MaxLength);
public sealed record StudioExternalTaskProfile(
    string ProfileRef, string ProfileVersion, string Topic, int MaxAttempts, int MaxDeadlineSeconds,
    IReadOnlyList<StudioExternalTaskField> Inputs, IReadOnlyList<StudioExternalTaskField> Outputs);
public sealed record StudioExternalTaskOperation(
    Guid JobId, Guid ProcessInstanceId, string ActivityId, string Topic, string? AgentProfileRef,
    string? AgentProfileVersion, string State, int AttemptsStarted, int MaxAttempts,
    long CreatedAt, long AvailableAt, long Deadline, long? LeaseExpiresAt, string? ErrorCode);
