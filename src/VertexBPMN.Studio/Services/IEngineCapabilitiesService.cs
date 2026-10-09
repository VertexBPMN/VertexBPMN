namespace VertexBPMN.Studio.Services;

internal interface IEngineCapabilitiesService
{
    EngineCapabilities? Current { get; }
    Task<EngineCapabilities> GetAsync(CancellationToken cancellationToken = default);
}
