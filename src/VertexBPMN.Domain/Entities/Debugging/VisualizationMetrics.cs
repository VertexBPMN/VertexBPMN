namespace VertexBPMN.Domain.Entities.Debugging;

public class VisualizationMetrics
{
    public int TotalActivities { get; set; }
    public int CompletedActivities { get; set; }
    public int ActiveActivities { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageActivityDuration { get; set; }
}