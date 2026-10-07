namespace VertexBPMN.Domain.Interfaces;

public sealed record FormDefinitionMetadata(string Id, string TenantId, string Key, string Name, string Schema, int Version, DateTime CreatedAt, DateTime LastModified);
