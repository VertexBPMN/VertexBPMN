namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Documentation for text descriptions (5.1.1.1, inherits from CMMNElement).
/// </summary>
public record Documentation(
    string Text,
    string? TextFormat = "text/plain"
) : CMMNElement();