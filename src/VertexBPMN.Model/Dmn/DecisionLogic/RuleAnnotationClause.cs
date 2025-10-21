using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionLogic;

/// <summary>
/// RuleAnnotationClause for decision table.
/// </summary>
public record RuleAnnotationClause(
    string Name
) : DMNElement();