namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record DataInput : ItemAwareElement
{
    public string? Name { get; set; }
    public bool? IsCollection { get; set; }
}