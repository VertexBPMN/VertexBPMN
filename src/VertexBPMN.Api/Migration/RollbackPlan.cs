namespace VertexBPMN.Api.Migration;

public class RollbackPlan
{
    public string Strategy { get; set; } = string.Empty;
    public TimeSpan EstimatedDuration { get; set; }
    public List<string> Steps { get; set; } = new();
}