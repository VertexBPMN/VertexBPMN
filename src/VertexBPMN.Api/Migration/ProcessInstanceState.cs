namespace VertexBPMN.Api.Migration;

public class ProcessInstanceState
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public Guid ProcessDefinitionId { get; set; }
    public string ProcessId { get; set; } = string.Empty;
}