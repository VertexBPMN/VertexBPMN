namespace VertexBPMN.Domain.Entities;

public sealed class EventStreamSubscription
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long ExpiredThrough { get; set; }
}
