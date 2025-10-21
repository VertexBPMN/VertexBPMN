namespace VertexBPMN.Domain.Model.Bpmn.Common;

public record TextAnnotation
(
    string? Text,
    string? TextFormat = "text/plain"
) :  Artifact();