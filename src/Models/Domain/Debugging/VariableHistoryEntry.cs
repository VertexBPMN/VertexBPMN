using System;

namespace VertexBPMN.Domain.Debugging;

public class VariableHistoryEntry
{
    public string VariableName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ActivityId { get; set; } = string.Empty;
}