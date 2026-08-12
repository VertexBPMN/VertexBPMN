using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Exceptions;

/// <summary>
/// Exception thrown when BPMN validation fails and ThrowOnFatalValidation is enabled.
/// Contains structured validation diagnostics for programmatic access.
/// </summary>
public class BpmnValidationException : Exception
{
    public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; }
    public List<string> Errors { get; }
    public BpmnValidationException(string message, List<string> errors) : base(message)
    {
        Errors = errors;
    }
    public BpmnValidationException(string message, IReadOnlyList<ValidationDiagnostic> diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public BpmnValidationException(string message, IReadOnlyList<ValidationDiagnostic> diagnostics, Exception innerException)
        : base(message, innerException)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }
    /// <summary>
    /// Creates a new validation exception with the given message, inner exception, and diagnostics.
    /// </summary>
    public BpmnValidationException(string message, Exception innerException, IReadOnlyList<ValidationDiagnostic> diagnostics)
        : base(message, innerException)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public override string ToString()
    {
        var diagnosticSummary = Diagnostics.Count > 0
            ? $"\nValidation Diagnostics ({Diagnostics.Count}):\n" +
              string.Join("\n", Diagnostics.Select(d => $"  - {d.Code}: {d.Message} [{d.Severity}]"))
            : "\nNo validation diagnostics available.";

        return base.ToString() + diagnosticSummary;
    }
}
