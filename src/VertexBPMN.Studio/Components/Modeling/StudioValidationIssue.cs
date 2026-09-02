namespace VertexBPMN.Studio.Components.Modeling;

public sealed record StudioValidationIssue(string Code, string Severity, string? ElementId, string Message);
