namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

#nullable enable

/// <summary>
/// Documentation element for text descriptions, as per Figure 8.5.
/// </summary>
public record Documentation(
    string Text,
    string? TextFormat = "text/plain"
);