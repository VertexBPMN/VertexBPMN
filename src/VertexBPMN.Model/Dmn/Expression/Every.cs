namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// Every (extends Quantified). Semantics: True if all satisfy; false/null otherwise.
/// Examples: `every x in [1..10] satisfies x > 0`.
/// </summary>
public record Every() : Quantified(Satisfies: new ChildExpression(Value: null!));