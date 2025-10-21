
namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// For (extends Iterator). 
/// Notation: Box with "for var in expr return ...". 
/// Semantics: Maps to list of return values.
/// Examples: `for i in 1..10 return i*2`; partial via `... partial: [1,2] ...`.
/// DMN 1.5: Partial variable; multi-iterator (for v1 in e1, v2 in e2).
/// </summary>
public record For(
    ChildExpression Return, // [1] Expression per iteration.
    ChildExpression? Partial = null // [0..1] Partial list (DMN 1.5).
) : Iterator(IteratorVariable: string.Empty, In: new TypedChildExpression(Value: null!));