using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionLogic;

/// <summary>
/// RuleAnnotation for decision table.
/// </summary>
public record RuleAnnotation(
    string? Text = null
) : DMNElement();