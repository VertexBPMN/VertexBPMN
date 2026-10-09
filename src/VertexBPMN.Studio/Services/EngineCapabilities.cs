namespace VertexBPMN.Studio.Services;

internal sealed record EngineCapabilities(
    string EngineType,
    bool SupportsCmmn,
    bool SupportsWorkers,
    bool SupportsDurablePersistence);
