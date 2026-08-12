namespace VertexBPMN.Domain.Model.Bpmn;

public readonly record struct ValidationDiagnostic(
    string Code,
    ValidationSeverity Severity,
    string Message,
    string? ElementId = null,
    string? Category = null
);