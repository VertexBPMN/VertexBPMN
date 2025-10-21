namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record DataObject : ItemAwareElement
{
    public string? Name { get; set; }
    public bool? IsCollection { get; set; }
}