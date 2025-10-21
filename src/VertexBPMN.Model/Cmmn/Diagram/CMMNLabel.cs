namespace VertexBPMN.Domain.Model.Cmmn.Diagram;

/// <summary>
/// CMMN label (Figure 7.6).
/// </summary>
public record CMMNLabel(
    Bounds? Bounds = null,
    CMMNStyle? LabelStyle = null
);