namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Represents a scheduled job (timer, async continuation, etc.).
/// </summary>
public class Job
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public int Retries { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TenantId { get; set; }
    public ProcessInstance ProcessInstance { get; set; } = null!;
    public string State { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? LockOwner { get; set; }
    public DateTime? LockedUntil { get; set; }
    public long Revision { get; set; }
}
