namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Represents an incident (error, failure, etc.) during process execution.
/// </summary>
public class Incident
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? TenantId { get; set; }
    public string? ActivityId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int RetryCount { get; set; }
    public ProcessInstance ProcessInstance { get; set; } = null!;
    public string State { get; set; } = "Open";
}
