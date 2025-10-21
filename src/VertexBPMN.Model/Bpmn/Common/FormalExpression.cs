namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Formal expression, as per Figure 8.21.
/// </summary>
public record FormalExpression(
    string? Language = null,
    ItemDefinition? EvaluatesToTypeRef = null
) : Expression();