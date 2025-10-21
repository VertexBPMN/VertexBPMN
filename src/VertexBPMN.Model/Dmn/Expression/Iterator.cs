namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// Abstract Iterator (Figure 10-27, extends Expression). 
/// Semantics: Iterates over list; partial variable support.
/// DMN 1.5: Ranges for dates/numbers; null handling.
/// </summary>
public abstract record Iterator(
    string IteratorVariable, // [1] Variable name (e.g., "item").
    TypedChildExpression In // [1] Iterable expression.
) : Core.Expression();