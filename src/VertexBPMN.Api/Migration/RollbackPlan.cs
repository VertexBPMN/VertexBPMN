namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type RollbackPlan retained for exposed HTTP action signatures and controller activation (RollbackPlan).")]
public class RollbackPlan
{
    public string Strategy { get; set; } = string.Empty;
    public TimeSpan EstimatedDuration { get; set; }
    public List<string> Steps { get; set; } = new();
}
