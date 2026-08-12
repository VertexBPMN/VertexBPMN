using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Di;

public class BpmnEdge : BaseElement
{
    public required BaseElement BpmnElement { get; set; }
    public IReadOnlyList<Point> Waypoints { get; } = [];
    public BpmnLabel? Label { get; set; }
}