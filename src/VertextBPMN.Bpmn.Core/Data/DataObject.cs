namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class DataObject : ItemAwareElement
{
    public string? Name { get; set; }
    public bool? IsCollection { get; set; }
}