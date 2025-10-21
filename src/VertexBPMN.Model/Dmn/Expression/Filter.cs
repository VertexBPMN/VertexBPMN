namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// Filter (Figure 10-27, extends Expression). 
/// Notation: Box with "in" top, match below. 
/// Semantics: Filters list by boolean expression; singletons as lists.
/// Examples: `applicant.expenses[category = "housing"]`.
/// DMN 1.5: Handles nulls; integrates with iterators.
/// </summary>
public record Filter(
    TypedChildExpression In, // [1] List expression.
    ChildExpression Match // [1] Filter condition (item variable implicit).
) : Core.Expression();