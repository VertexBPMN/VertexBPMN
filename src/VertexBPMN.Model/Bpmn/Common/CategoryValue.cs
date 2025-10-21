using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

public record CategoryValue : BaseElement
{
    public string? Value { get; set; }
    public Category? Category { get; set; }
}