using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// AuthorityRequirement (extends DMNElement).
/// </summary>
public record AuthorityRequirement(
    KnowledgeSource? RequiredAuthority = null,
    Decision? RequiredDecision = null,
    InputData? RequiredInput = null
) : DMNElement();