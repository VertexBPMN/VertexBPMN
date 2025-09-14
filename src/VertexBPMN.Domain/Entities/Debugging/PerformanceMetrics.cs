namespace VertexBPMN.Domain.Entities.Debugging;

public class PerformanceMetrics
{
    public int TotalEvents { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public DateTime? FastestEventTime { get; set; }
    public DateTime? SlowestEventTime { get; set; }
}