using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.Diagram;

/// <summary>
/// CMMN diagram (Figure 7.2, inherits from DI::Diagram).
/// Extension: Added depiction resolutions.
/// </summary>
public record CMMNDiagram(
    string Name = "",
    string Documentation = "",
    double Resolution = 300.0,
    CMMNElement? CmmnElementRef = null,
    CMMNStyle? SharedStyle = null,
    CMMNStyle? LocalStyle = null,
    Dimension? Size = null,
    List<CMMNDiagramElement> DiagramElements = null!
);