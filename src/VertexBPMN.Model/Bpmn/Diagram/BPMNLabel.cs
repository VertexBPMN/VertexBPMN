using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Diagram;

#nullable enable

/// <summary>
/// BPMN label, as per BPMNDI.
/// </summary>
public record BPMNLabel(
    Bounds? Bounds = null,
    BPMNLabelStyle? LabelStyle = null
) : BaseElement;