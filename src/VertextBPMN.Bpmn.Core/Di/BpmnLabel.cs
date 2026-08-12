using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Di;

public class BpmnLabel : BaseElement
{
    public Bounds? Bounds { get; set; }
    public BpmnLabelStyle? LabelStyle { get; set; }
}