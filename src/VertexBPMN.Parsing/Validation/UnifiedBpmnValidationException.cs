namespace VertexBPMN.Parsing;

public sealed class UnifiedBpmnValidationException : Exception
{
    public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; }

    public UnifiedBpmnValidationException(string message, IReadOnlyList<ValidationDiagnostic> diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics;
    }
}