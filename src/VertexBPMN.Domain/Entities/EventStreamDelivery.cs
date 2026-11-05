namespace VertexBPMN.Domain.Entities;

// A durable read projection, not proof of any business effect.
public sealed class EventStreamDelivery
{
    public long Id { get; set; }
    public DateTime RetainedAt { get; set; } = DateTime.UtcNow;
    public Guid SubscriptionId { get; set; }
    public Guid SourceEventId { get; set; }
    public Guid? DefinitionId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}
