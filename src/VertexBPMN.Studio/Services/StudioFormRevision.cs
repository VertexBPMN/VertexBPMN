namespace VertexBPMN.Studio.Services;

internal sealed record StudioFormRevision(string Id, string FormDefinitionId, string TenantId, string Key, string Name, string Schema, int Version, string SchemaHash, DateTime CreatedAt);
