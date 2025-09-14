namespace VertexBPMN.Domain.Entities.Debugging;

public class ContinueResult
{
    public Guid SessionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string StartActivity { get; set; } = string.Empty;
    public string EndActivity { get; set; } = string.Empty;
    public bool BreakpointHit { get; set; }
    public bool ProcessCompleted { get; set; }
}