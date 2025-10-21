namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Unary tests (extends Expression). DMN 1.5: Generalized with '?' placeholders.
/// </summary>
public record UnaryTests(
    string? Text = null,
    string? ExpressionLanguage = null
) : Expression();