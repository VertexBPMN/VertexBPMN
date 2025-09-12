using Microsoft.ML.Data;

namespace VertexBPMN.Domain.ML;

public class DurationPredictionOutput
{
    [ColumnName("Score")]
    public float EstimatedDurationMinutes { get; set; }
    public float ConfidenceScore { get; set; }
}