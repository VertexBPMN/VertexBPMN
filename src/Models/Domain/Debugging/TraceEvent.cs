using System;

namespace VertexBPMN.Domain.Debugging;

public class TraceEvent
{
    public string Type { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
    public TimeSpan? Duration { get; set; }
}