namespace VertexBPMN.Domain.Model.Dmn.Expression;

#nullable enable

/// <summary>
/// Context (Figure 10-27, extends Expression). 
/// Notation: Boxed rows (key left, expr right); jagged for tables. 
/// Semantics: `{k1: v1, ..., result: r}`; acyclic evaluation; QN access.
/// Examples: Applicant risk assessment `{age: applicant.age, riskScore: if age > 60 then "high" else "low"}` (Figure 10-28).
/// DMN 1.5: Builtins like `context put`; sums/filtered lists handle nulls.
/// </summary>
public record Context(
    List<ContextEntry> ContextEntries = null!, // [1..*] Entries; evaluated in partial order.
    string? Text = null // [0..1] FEEL text (e.g., `{key1: value1}`).
) : Core.Expression();