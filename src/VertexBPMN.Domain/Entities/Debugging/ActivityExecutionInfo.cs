namespace VertexBPMN.Domain.Entities.Debugging;

public class ActivityExecutionInfo
{
    public string ActivityId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public int ExecutionCount { get; set; }
}