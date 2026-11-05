namespace VertexBPMN.Sdk;

public sealed record FormDefinitionRevisionMetadata(string Id, string FormDefinitionId, string TenantId,
    string Key, string Name, string Schema, string SchemaHash, int Version, DateTime CreatedAt);
