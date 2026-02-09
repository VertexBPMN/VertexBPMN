namespace VertexBPMN.Domain.Model.Security;

public sealed record ComplexityAnalysisResult
{
    public int ElementCount { get; init; }
    public int AttributeCount { get; init; }
    public int NestingDepth { get; init; }
    public double ComplexityScore { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public string Description { get; init; } = string.Empty;
}