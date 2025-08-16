namespace VertexBPMN.Core.Bpmn;

public record BpmnTask(string Id, string Type, string? Implementation = null,
    IDictionary<string, string>? Attributes = null)
{
    public string Name { get; init; } = string.Empty;   
}