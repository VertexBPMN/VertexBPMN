namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// Conditional (Figure 10-27, extends Expression). 
/// Notation: Box divided into if/then/else compartments. 
/// Semantics: if e1 then e2 else e3; ternary logic (true/false/null).
/// Examples: `if age > 60 then "high" else "low"` (Figure 10-28).
/// DMN 1.5: Null handling; implicit conversions.
/// </summary>
public record Conditional(
    ChildExpression If, // [1] Condition.
    ChildExpression Then, // [1] True branch.
    ChildExpression? Else = null // [0..1] False branch.
) : Core.Expression();