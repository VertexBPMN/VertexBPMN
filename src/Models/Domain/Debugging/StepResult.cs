using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain.Debugging;

public class StepResult
{
    public StepType Type { get; set; }
    public string StartActivity { get; set; } = string.Empty;
    public string EndActivity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool BreakpointHit { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
}