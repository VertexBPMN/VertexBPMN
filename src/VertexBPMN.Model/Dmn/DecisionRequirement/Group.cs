namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// Group (extends Artifact).
/// </summary>
public record Group(
    string? Name = null
) : Artifact();