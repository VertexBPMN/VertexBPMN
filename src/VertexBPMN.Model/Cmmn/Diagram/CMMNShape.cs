using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.Diagram;

/// <summary>
/// CMMN shape (Figure 7.4, inherits from CMMNDiagramElement).
/// Extension: Added decorator flags.
/// </summary>
public record CMMNShape(
    Bounds Bounds,
    CMMNElement CmmnElementRef,
    bool? IsCollapsed = false,
    bool? IsPlanningTableCollapsed = false,
    bool? AutoCompleteDecorator = false, // Extension: Depiction resolution.
    bool? ItemControlDecorator = false // Extension: For rules.
) : CMMNDiagramElement();