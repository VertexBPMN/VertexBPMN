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