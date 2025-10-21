using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// ChildExpression.
/// </summary>
public record ChildExpression(
    Core.Expression Value
) : DMNElement();