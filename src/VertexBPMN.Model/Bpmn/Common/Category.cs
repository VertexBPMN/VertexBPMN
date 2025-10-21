using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

public record Category : RootElement
{
    public string? Name { get; set; }
    public List<CategoryValue> CategoryValues { get; } = [];
}