namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Literal expression (extends Expression). 
/// Notation: Boxed text. Semantics: Evaluates per language; typographical literals (e.g., italic strings).
/// Examples: `age > 30`; *DECLINED* for strings.
/// DMN 1.5: Level 2 S-FEEL; Level 3 full FEEL; no nesting.
/// </summary>
public record LiteralExpression(
    string? Text = null, // [0..1] Expression text.
    string? ExpressionLanguage = null,
    ImportedValues? ImportedValues = null // [0..1] External reference; mutually exclusive with Text.
) : Expression();