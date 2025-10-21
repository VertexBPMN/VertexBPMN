using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.DI;

public abstract class DcStyle : Style
{
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public string? FontColor { get; set; }
    public string? StrokeColor { get; set; }
    public string? FillColor { get; set; }
}