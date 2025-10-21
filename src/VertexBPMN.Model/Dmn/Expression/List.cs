namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// List (Figure 10-27, extends Expression). 
/// Notation: Vertical/horizontal boxed items. 
/// Semantics: Ordered [e1, e2, ...]; supports indexing/filtering.
/// Examples: `[1, 2, 3]`; filtered `applicant.expenses[category = "housing"]`.
/// DMN 1.5: Implicit conversions; null handling in builtins.
/// </summary>
public record List(
    List<Core.Expression> Elements = null!, // [0..*] Nested expressions.
    string? Text = null // [0..1] FEEL text (e.g., `[1,2,3]`).
) : Core.Expression();