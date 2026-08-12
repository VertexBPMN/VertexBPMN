using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Di;

public class BpmnDiagram : BaseElement
{
    public required BpmnPlane Plane { get; set; }
    public IReadOnlyList<BpmnLabelStyle> LabelStyles { get; } = [];
}