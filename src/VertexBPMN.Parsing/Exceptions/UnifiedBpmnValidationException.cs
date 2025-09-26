using System;
using System.Collections.Generic;
using System.Linq;

namespace VertexBPMN.Parsing;

/// <summary>
/// Exception thrown when BPMN validation fails and ThrowOnFatalValidation is enabled.
/// Contains structured validation diagnostics for programmatic access.
/// </summary>
public class UnifiedBpmnValidationException : Exception
{
    public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; }

    public UnifiedBpmnValidationException(string message, IReadOnlyList<ValidationDiagnostic> diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public UnifiedBpmnValidationException(string message, IReadOnlyList<ValidationDiagnostic> diagnostics, Exception innerException)
        : base(message, innerException)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }
    /// <summary>
    /// Creates a new validation exception with the given message, inner exception, and diagnostics.
    /// </summary>
    public UnifiedBpmnValidationException(string message, Exception innerException, IReadOnlyList<ValidationDiagnostic> diagnostics)
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