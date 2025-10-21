using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.Enums;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// Association (extends Artifact).
/// </summary>
public record Association(
    DMNElement SourceRef,
    DMNElement TargetRef,
    AssociationDirection AssociationDirection = AssociationDirection.None
) : Artifact();