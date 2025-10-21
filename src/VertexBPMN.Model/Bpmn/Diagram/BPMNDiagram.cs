using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Diagram;

#nullable enable

/// <summary>
/// BPMN diagram, as per BPMNDI.
/// </summary>
public record BPMNDiagram(
    string Name,
    BPMNPlane BPMNPlane,
    List<BPMNLabelStyle> BPMNLabelStyles = null!
) : BaseElement;