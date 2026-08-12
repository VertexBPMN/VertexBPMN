using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>
/// Exception thrown when BPMN XML schema validation fails.
/// </summary>
public sealed class BpmnSchemaValidationException : Exception
{
    /// <summary>
    /// Collected schema validation diagnostics (warnings and errors).
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>
    /// Creates a new instance of <see cref="BpmnSchemaValidationException"/> from raw string diagnostics.
    /// </summary>
    /// <param name="diagnostics">Validation diagnostics produced during schema validation.</param>
    public BpmnSchemaValidationException(IEnumerable<string> diagnostics)
        : base(BuildMessage(diagnostics))
    {
        Diagnostics = diagnostics?.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray() ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates a new instance of <see cref="BpmnSchemaValidationException"/> from structured diagnostics.
    /// </summary>
    /// <param name="validationDiagnostics">Structured validation diagnostics.</param>
    public BpmnSchemaValidationException(List<ValidationDiagnostic> validationDiagnostics)
        : base(BuildMessage(validationDiagnostics?.Select(FormatDiagnostic)!))
    {
        Diagnostics = validationDiagnostics?
            .Where(d => d is not null)
            .Select(FormatDiagnostic)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray() ?? Array.Empty<string>();
    }

    private static string FormatDiagnostic(ValidationDiagnostic d)
    {
        // Gracefully handle unexpected nulls or missing data.
        var severity = d?.Severity.ToString() ?? "Unknown";
        var message = d?.Message;
        return string.IsNullOrWhiteSpace(message) ? severity : $"{severity}: {message}";
    }

    private static string BuildMessage(IEnumerable<string>? diagnostics)
    {
        if (diagnostics is null) return "BPMN schema validation failed.";
        var list = diagnostics.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        if (list.Count == 0) return "BPMN schema validation failed.";
        var previewCount = Math.Min(5, list.Count);
        var preview = string.Join("; ", list.Take(previewCount));
        return list.Count > previewCount
            ? $"BPMN schema validation failed with {list.Count} diagnostics (showing {previewCount}): {preview}"
            : $"BPMN schema validation failed with {list.Count} diagnostics: {preview}";
    }
}