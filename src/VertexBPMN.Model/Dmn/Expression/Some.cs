namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// Some (extends Quantified). Semantics: True if at least one satisfies.
/// Examples: `some x in xs satisfies x.age > 60`.
/// </summary>
public record Some() : Quantified(Satisfies: new ChildExpression(Value: null!));