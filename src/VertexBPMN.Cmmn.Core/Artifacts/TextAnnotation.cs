using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.Artifacts;

public sealed class TextAnnotation : CmmnElement
{
    public string? Text { get; set; }
}