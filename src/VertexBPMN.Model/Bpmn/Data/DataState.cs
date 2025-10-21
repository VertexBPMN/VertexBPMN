using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record DataState : BaseElement
{
    public string? Name { get; set; }
}