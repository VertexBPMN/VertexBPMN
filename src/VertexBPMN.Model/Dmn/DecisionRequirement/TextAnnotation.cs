namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// TextAnnotation (extends Artifact).
/// </summary>
public record TextAnnotation(
    string Text,
    string TextFormat = "text/plain"
) : Artifact();