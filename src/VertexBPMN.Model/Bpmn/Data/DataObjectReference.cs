namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record DataObjectReference : ItemAwareElement
{
    public DataObject? DataObjectRef { get; set; }
    public string? Name { get; set; }
}