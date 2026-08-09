namespace VertexBPMN.Domain.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Resource { get; set; }
    public string? ResourceId { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? CorrelationId { get; set; }
    public int StatusCode { get; set; }
    public string? DetailsJson { get; set; }
}