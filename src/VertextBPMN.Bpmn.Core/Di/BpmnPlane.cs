using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Di;

public class BpmnPlane : BaseElement
{
    public required BaseElement BpmnElement { get; set; }
    public IReadOnlyList<BpmnShape> Shapes { get; } = [];
    public IReadOnlyList<BpmnEdge> Edges { get; } = [];
}