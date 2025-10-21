using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Diagram;

#nullable enable

/// <summary>
/// BPMN plane, as per BPMNDI.
/// </summary>
public record BPMNPlane(
    BaseElement BpmnElement
) : BaseElement
{
    public List<BPMNShape> Shapes { get; set; } = [];
    public List<BPMNEdge> Edges { get; set; } = [];
}