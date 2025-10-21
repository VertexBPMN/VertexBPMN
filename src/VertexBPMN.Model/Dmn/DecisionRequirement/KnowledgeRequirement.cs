using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// KnowledgeRequirement (extends DMNElement).
/// </summary>
public record KnowledgeRequirement(
    Invocable RequiredKnowledge
) : DMNElement();