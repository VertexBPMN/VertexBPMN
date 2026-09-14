namespace VertexBPMN.Domain.Entities;

public enum ExternalTaskState { Ready, Leased, RetryScheduled, Completed, Failed, Cancelled, TimedOut }

/// <summary>One durable external activity execution. Times are UTC Unix milliseconds.</summary>
public sealed class ExternalTaskJob
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ProcessInstanceId { get; set; }
    public Guid DefinitionId { get; set; }
    public int DefinitionVersion { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public Guid ActivityExecutionId { get; set; }
    public Guid WaitTokenId { get; set; }
    public Guid ScopeExecutionId { get; set; }
    public Guid? MultiInstanceExecutionId { get; set; }
    public int? MultiInstanceIndex { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = string.Empty;
    public string? AgentProfileRef { get; set; }
    public string? AgentProfileVersion { get; set; }
    public string InputSnapshot { get; set; } = "{}";
    public string DefinitionSnapshot { get; set; } = "{}";
    public string SchemaSnapshot { get; set; } = "{}";
    public ExternalTaskState State { get; set; }
    public long Revision { get; set; }
    public long CreatedAt { get; set; }
    public long AvailableAt { get; set; }
    public long Deadline { get; set; }
    public int AttemptsStarted { get; set; }
    public int MaxAttempts { get; set; }
    public Guid? LeaseId { get; set; }
    public long LeaseGeneration { get; set; }
    public long? LeaseExpiresAt { get; set; }
    public string? WorkerIssuer { get; set; }
    public string? WorkerSubject { get; set; }
    public string? Result { get; set; }
    public Guid? CompletionId { get; set; }
    public string? ResultHash { get; set; }
    public long? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
}
