using System;

namespace VertexBPMN.Domain.ML;

public class ActivityBottleneckPrediction
{
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public float BottleneckProbability { get; set; }
    public float AverageWaitTime { get; set; }
    public float ThroughputImpact { get; set; }
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
}