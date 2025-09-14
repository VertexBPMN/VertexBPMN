namespace VertexBPMN.Domain.Entities.ML;

public class ProcessDurationPrediction
{
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public float EstimatedDurationMinutes { get; set; }
    public float MinDuration { get; set; }
    public float MaxDuration { get; set; }
    public float ConfidenceScore { get; set; }
    public string[] InfluencingFactors { get; set; } = Array.Empty<string>();
    public string[] SuggestedOptimizations { get; set; } = Array.Empty<string>();
}