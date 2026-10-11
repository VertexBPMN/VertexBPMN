namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type MigrationCompatibilityIssue retained for exposed HTTP action signatures and controller activation (MigrationCompatibilityIssue).")]
public class MigrationCompatibilityIssue
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string AffectedElement { get; set; } = string.Empty;
}
