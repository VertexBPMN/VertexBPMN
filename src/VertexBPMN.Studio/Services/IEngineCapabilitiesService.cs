namespace VertexBPMN.Studio.Services;

public interface IEngineCapabilitiesService
{
    EngineCapabilities? Current { get; }
    Task<EngineCapabilities> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record EngineCapabilities(
    string EngineType,
    bool SupportsCmmn,
    bool SupportsWorkers,
    bool SupportsDurablePersistence);
