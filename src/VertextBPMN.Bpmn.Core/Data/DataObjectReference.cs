namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class DataObjectReference : ItemAwareElement
{
    public DataObject? DataObjectRef { get; set; }
    public string? Name { get; set; }
}