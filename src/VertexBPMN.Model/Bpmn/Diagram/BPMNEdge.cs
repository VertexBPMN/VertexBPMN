using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Diagram;

#nullable enable

/// <summary>
/// BPMN edge, as per BPMNDI.
/// </summary>
public record BPMNEdge(
    BaseElement BpmnElement,
    List<Point> WayPoints,
    BPMNLabel? BPMNLabel = null
) : BaseElement
{
    public BPMNEdge(BaseElement bpmnElement, BPMNLabel? bpmnLabel = null)
        : this(bpmnElement, new List<Point>(), bpmnLabel)
    {
    }
}