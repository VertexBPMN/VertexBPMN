using VertexBPMN.Domain.Entities.Debugging;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Loads a persisted process instance projection for visual debugging.
/// </summary>
public interface IProcessVisualizationService
{
    Task<ProcessVisualization> GetAsync(
        Guid processInstanceId,
        CancellationToken cancellationToken = default);
}
