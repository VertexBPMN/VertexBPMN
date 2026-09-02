using Microsoft.ML.Data;

namespace VertexBPMN.Domain.Entities.ML;

public class CompletionPredictionOutput
{
    [ColumnName("Score")]
    public float CompletionProbability { get; set; }
    public float EstimatedMinutesToCompletion { get; set; }
    public float ConfidenceScore { get; set; }
}