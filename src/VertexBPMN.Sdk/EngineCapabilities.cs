namespace VertexBPMN.Sdk;

public sealed record EngineCapabilities(
    VertexBpmnEngineType EngineType,
    bool SupportsCmmn,
    bool SupportsWorkers,
    bool SupportsDurablePersistence);
