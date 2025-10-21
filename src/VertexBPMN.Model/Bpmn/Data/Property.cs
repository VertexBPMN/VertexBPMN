namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record Property : ItemAwareElement
{
    public string? Name { get; set; }
}