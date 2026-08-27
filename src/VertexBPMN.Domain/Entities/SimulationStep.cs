namespace VertexBPMN.Domain.Entities;

public class SimulationStep
{
    public int StepNumber { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string? IncomingFlowId { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
