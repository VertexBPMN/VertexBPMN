namespace VertexBPMN.Domain.Model.Dmn.Artifacts;

/// <summary>TextAnnotation (6.3.6.3)</summary>
public sealed class TextAnnotation : Artifact
{
    public string Text { get; set; } = string.Empty;
    public string TextFormat { get; set; } = "text/plain";
}