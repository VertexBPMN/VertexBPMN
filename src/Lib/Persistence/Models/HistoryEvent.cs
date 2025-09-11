using System;

namespace VertexBPMN.Persistence.Models;

public class HistoryEvent
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public ProcessInstance ProcessInstance { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public string ElementId { get; set; } = string.Empty;
    public string? Data { get; set; }
    public DateTime Timestamp { get; set; }
}
