using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Diagram;

#nullable enable

/// <summary>
/// BPMN label style, as per BPMNDI.
/// </summary>
public record BPMNLabelStyle(
    string Font
) : BaseElement;