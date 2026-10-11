namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type MigrationStepResult retained for exposed HTTP action signatures and controller activation (MigrationStepResult).")]
public class MigrationStepResult
{
    public string StepName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
}
