namespace VertexBPMN.Domain.Model.Cmmn.Diagram;

/// <summary>
/// CMMN style (Figure 7.7).
/// </summary>
public record CMMNStyle(
    Color? FillColor = null,
    Color? StrokeColor = null,
    Color? FontColor = null,
    string? FontFamily = null,
    double? FontSize = null,
    bool? FontItalic = null,
    bool? FontBold = null,
    bool? FontUnderline = null,
    bool? FontStrikeThrough = null,
    double? FontWeight = null,
    string? LabelHorizontalAlignment = null,
    string? LabelVerticalAlignment = null
);