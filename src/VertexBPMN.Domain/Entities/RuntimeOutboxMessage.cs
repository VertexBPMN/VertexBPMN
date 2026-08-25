namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Transactional record of a durable runtime state transition.
/// </summary>
public sealed class RuntimeOutboxMessage
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public string State { get; set; } = "Pending";
    public string? TenantId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LockOwner { get; set; }
    public DateTime? LockedUntil { get; set; }
}
