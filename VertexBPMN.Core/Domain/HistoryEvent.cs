namespace VertexBPMN.Core.Domain;

/// <summary>
/// Represents a historical event for audit and compliance.
/// </summary>
public class HistoryEvent
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
    public string? TenantId { get; set; }
    // TODO: Add event-specific properties
}
