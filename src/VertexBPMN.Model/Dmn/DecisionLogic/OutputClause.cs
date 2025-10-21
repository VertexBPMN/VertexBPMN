using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionLogic;

/// <summary>
/// OutputClause for decision table.
/// </summary>
public record OutputClause(
    string? TypeRef = null,
    string? Name = null,
    UnaryTests? OutputValues = null,
    Core.Expression? DefaultOutputEntry = null,
    List<LiteralExpression> OutputEntries = null!
) : DMNElement();