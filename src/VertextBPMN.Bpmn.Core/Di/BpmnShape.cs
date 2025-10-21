using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Di;

public class BpmnShape : BaseElement
{
    public required BaseElement BpmnElement { get; set; }
    public Bounds? Bounds { get; set; }
    public BpmnLabel? Label { get; set; }
    public bool? IsExpanded { get; set; }
    public bool? IsMarkerVisible { get; set; }
    public bool? IsHorizontal { get; set; }
}