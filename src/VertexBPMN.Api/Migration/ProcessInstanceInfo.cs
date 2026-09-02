namespace VertexBPMN.Api.Migration;

public class ProcessInstanceInfo
{
    public Guid Id { get; set; }
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}