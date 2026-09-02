using VertexBPMN.Domain.Entities.Debugging;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Advances a persisted process instance by one visual-debug step.
/// </summary>
public interface IVisualDebugStepService
{
    Task<VisualDebugStepResult> StepAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
}
