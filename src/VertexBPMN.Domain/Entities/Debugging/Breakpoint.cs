namespace VertexBPMN.Domain.Entities.Debugging;

public class Breakpoint
{
    public Guid Id { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public BreakpointCondition? Condition { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastHitAt { get; set; }
    public bool IsEnabled { get; set; }
    public int HitCount { get; set; }
}