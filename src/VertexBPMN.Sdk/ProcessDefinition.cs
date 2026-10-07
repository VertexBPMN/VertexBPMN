namespace VertexBPMN.Sdk;

public sealed record ProcessDefinition(
    string Id,
    string Key,
    string Name,
    int Version,
    string TenantId,
    bool Suspended);
