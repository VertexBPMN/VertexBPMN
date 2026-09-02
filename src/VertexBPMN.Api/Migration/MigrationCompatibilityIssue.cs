namespace VertexBPMN.Api.Migration;

public class MigrationCompatibilityIssue
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string AffectedElement { get; set; } = string.Empty;
}