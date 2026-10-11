namespace VertexBPMN.Studio.Components.Modeling;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "The public Razor editor component returns these validation results to its callers.")]
public sealed record StudioValidationIssue(string Code, string Severity, string? ElementId, string Message);
