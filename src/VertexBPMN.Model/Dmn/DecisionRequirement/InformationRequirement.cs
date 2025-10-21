using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// InformationRequirement (extends DMNElement).
/// </summary>
public record InformationRequirement(
    Decision? RequiredDecision = null,
    InputData? RequiredInput = null
) : DMNElement();