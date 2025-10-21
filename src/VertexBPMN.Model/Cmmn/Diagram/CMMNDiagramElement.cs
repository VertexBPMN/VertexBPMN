using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.Diagram;

/// <summary>
/// Abstract CMMN diagram element (Figure 7.3).
/// </summary>
public abstract record CMMNDiagramElement(
    CMMNElement? CmmnElementRef = null,
    CMMNStyle? SharedStyle = null,
    CMMNStyle? LocalStyle = null,
    CMMNLabel? Label = null
);