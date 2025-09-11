using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain.Debugging;

public class VariableInspection
{
    public Guid SessionId { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string CurrentActivityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, VariableDetail> GlobalVariables { get; set; } = new();
    public Dictionary<string, VariableDetail> LocalVariables { get; set; } = new();
    public List<VariableHistoryEntry> VariableHistory { get; set; } = new();
}