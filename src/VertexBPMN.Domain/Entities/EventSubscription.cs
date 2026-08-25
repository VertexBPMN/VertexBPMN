namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Durable message or signal wait-state owned by one execution token.
/// </summary>
public sealed class EventSubscription
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public Guid ExecutionTokenId { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string State { get; set; } = "Active";
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConsumedAt { get; set; }
    public long Revision { get; set; }
}
