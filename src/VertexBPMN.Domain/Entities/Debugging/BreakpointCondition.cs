namespace VertexBPMN.Domain.Entities.Debugging;

public class BreakpointCondition
{
    public string? VariableName { get; set; }
    public string Operator { get; set; } = string.Empty; // equals, not_equals, greater_than, less_than
    public string Value { get; set; } = string.Empty;
    public int HitCount { get; set; } = 0; // Break on Nth hit
    public string? Expression { get; set; } // Custom expression
}