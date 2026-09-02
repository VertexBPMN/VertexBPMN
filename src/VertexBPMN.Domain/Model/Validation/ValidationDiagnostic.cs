namespace VertexBPMN.Domain.Model.Validation;

/// <summary>
/// Represents a validation diagnostic with structured information.
/// Phase 3: Advanced validation infrastructure.
/// </summary>
public sealed record ValidationDiagnostic(
    string Code,
    ValidationSeverity Severity,
    string Message,
    string? ElementId,
    string Category);

/// <summary>
/// Validation severity levels for structured diagnostics.
/// </summary>
public enum ValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Fatal = 3
}