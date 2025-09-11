using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain.Debugging;

public class ExecutionTrace
{
    public Guid SessionId { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<TraceEvent> Events { get; set; } = new();
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();
}