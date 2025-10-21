using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionLogic;

/// <summary>
/// InputClause for decision table.
/// </summary>
public record InputClause(
    Core.Expression? InputExpression = null,
    UnaryTests? InputValues = null,
    List<UnaryTests> InputEntries = null!
) : DMNElement();