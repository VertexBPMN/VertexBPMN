namespace VertexBPMN.Domain.Model.Bpmn;

public sealed class BpmnDeploymentValidationException(IReadOnlyList<ValidationDiagnostic> diagnostics)
    : InvalidOperationException($"The BPMN model is not executable: {string.Join("; ", diagnostics.Select(d => d.Message))}")
{
    public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; } = diagnostics;
}
