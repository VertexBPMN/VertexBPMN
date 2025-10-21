using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.Diagram;

/// <summary>
/// CMMN edge (Figure 7.5, inherits from CMMNDiagramElement).
/// Extension: Added event visibility.
/// </summary>
public record CMMNEdge(
    List<Point> WayPoints,
    CMMNElement? CmmnElementRef = null,
    CMMNElement? SourceCmmnElementRef = null,
    CMMNElement? TargetCmmnElementRef = null,
    bool? IsStandardEventVisible = false
) : CMMNDiagramElement();