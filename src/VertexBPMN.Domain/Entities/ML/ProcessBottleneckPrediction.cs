using System.Collections.Generic;

namespace VertexBPMN.Domain.ML;

public class ProcessBottleneckPrediction
{
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public float OverallBottleneckRisk { get; set; }
    public List<ActivityBottleneckPrediction> ActivityPredictions { get; set; } = new();
    public List<string> CriticalPath { get; set; } = new();
    public string OptimizationPriority { get; set; } = string.Empty;
}