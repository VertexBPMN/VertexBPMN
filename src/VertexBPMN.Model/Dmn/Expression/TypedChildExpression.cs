namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// TypedChildExpression (extends ChildExpression).
/// </summary>
public record TypedChildExpression(
    Core.Expression Value
) : ChildExpression(Value: Value);