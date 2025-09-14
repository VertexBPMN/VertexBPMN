namespace VertexBPMN.Domain.Entities.ML;

public class ProcessCompletionPrediction
{
    public Guid ProcessInstanceId { get; set; }
    public float CompletionProbability { get; set; }
    public DateTime EstimatedCompletionTime { get; set; }
    public float ConfidenceScore { get; set; }
    public string[] RiskFactors { get; set; } = Array.Empty<string>();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}