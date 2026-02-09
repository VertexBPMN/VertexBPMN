namespace VertexBPMN.Domain.Entities.Modeling;

public record BpmnTask(string Id, string Type, string? Implementation = null,
    Dictionary<string, string>? Attributes = null)
{
    public string Name { get; init; } = string.Empty;   
}