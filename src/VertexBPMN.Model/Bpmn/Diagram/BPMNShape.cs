
using VertexBPMN.Domain.Model.Bpmn.Enums;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Diagram;

#nullable enable

/// <summary>
/// BPMN shape, as per BPMNDI.
/// </summary>
public record BPMNShape(
    BaseElement BpmnElement,
    BPMNLabel? BPMNLabel = null,
    bool IsHorizontal = false,
    bool IsExpanded = false,
    bool IsMarkerVisible = false,
    bool IsMessageVisible = false,
    ChoreographyActivityShape? ParticipantBandKind = null
) : BaseElement
{
    public Bounds Bounds { get; set; }
}