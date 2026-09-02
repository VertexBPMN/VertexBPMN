namespace VertexBPMN.Domain.Entities.ML;

public class ActivityData
{
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public float AverageExecutionTime { get; set; }
    public int ExecutionCount { get; set; }
    public float ErrorRate { get; set; }
    public float BottleneckProbability { get; set; }
}