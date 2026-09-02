using Microsoft.ML.Data;

namespace VertexBPMN.Domain.Entities.ML;

public class BottleneckPredictionOutput
{
    [ColumnName("Score")]
    public float BottleneckProbability { get; set; }
    public float AverageWaitTime { get; set; }
    public float ThroughputImpact { get; set; }
}