

namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// Abstract Quantified (extends Iterator). 
/// Semantics: Boolean aggregation over iterations.
/// DMN 1.5: Ternary logic (true/false/null); some = any true, every = all true.
/// </summary>
public abstract record Quantified(
    ChildExpression Satisfies // [1] Boolean condition per item.
) : Iterator(IteratorVariable: string.Empty, In: new TypedChildExpression(Value: null!));