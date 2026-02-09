namespace VertexBPMN.Domain.Entities.ML;

public class OptimizationAction
{
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty;
}