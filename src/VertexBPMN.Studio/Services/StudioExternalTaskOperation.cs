namespace VertexBPMN.Studio.Services;
internal sealed record StudioExternalTaskOperation(
    Guid JobId, Guid ProcessInstanceId, string ActivityId, string Topic, string? AgentProfileRef,
    string? AgentProfileVersion, string State, int AttemptsStarted, int MaxAttempts,
    long CreatedAt, long AvailableAt, long Deadline, long? LeaseExpiresAt, string? ErrorCode);
