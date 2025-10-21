namespace VertexBPMN.Domain.Model.Bpmn.Common.Artifacts;

public class TextAnnotation : Artifact
{
    public string? Text { get; set; }
    public string? TextFormat { get; set; } = "text/plain";
}