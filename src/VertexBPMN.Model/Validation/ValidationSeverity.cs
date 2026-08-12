namespace VertexBPMN.Domain.Model.Validation;

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