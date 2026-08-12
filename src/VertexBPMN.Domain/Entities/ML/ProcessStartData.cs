namespace VertexBPMN.Domain.Entities.ML;

public class ProcessStartData
{
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public int VariableCount { get; set; }
    public bool HasBusinessKey { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int StartHour { get; set; }
    public int StartDayOfWeek { get; set; }
    public float EstimatedDurationMinutes { get; set; }
}