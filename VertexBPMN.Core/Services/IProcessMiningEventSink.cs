namespace VertexBPMN.Core.Services;

public interface IProcessMiningEventSink
{
    ValueTask EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default);
}

public class ProcessMiningEvent
{
    public int Id { get; set; } // EF Core primary key
    public string EventType { get; set; } = string.Empty;
    public string ProcessInstanceId { get; set; } = string.Empty;
    public string? TaskId { get; set; }
    public string? ActivityId { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? PayloadJson { get; set; } // Store payload as JSON string
}
