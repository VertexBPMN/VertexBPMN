namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Idempotency record for externally initiated runtime transitions.
/// </summary>
public sealed class RuntimeInboxMessage
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string TenantScope { get; set; } = "$global";
    public string? Result { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
