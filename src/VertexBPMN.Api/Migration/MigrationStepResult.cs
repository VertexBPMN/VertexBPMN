namespace VertexBPMN.Api.Migration;

public class MigrationStepResult
{
    public string StepName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
}