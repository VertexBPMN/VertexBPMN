namespace VertexBPMN.Domain.Entities.ML;

public class ProcessInstanceData
{
    public string ProcessInstanceId { get; set; } = string.Empty;
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public int VariableCount { get; set; }
    public float DurationMinutes { get; set; }
    public int ActivityCount { get; set; }
    public float CompletionProbability { get; set; }
}