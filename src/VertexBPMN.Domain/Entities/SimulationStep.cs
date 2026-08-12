namespace VertexBPMN.Domain.Entities;

public class SimulationStep
{
    public int StepNumber { get; set; }
    public string ActivityId { get; set; }
    public string ActivityName { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public DateTime Timestamp { get; set; }
}