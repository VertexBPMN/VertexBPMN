namespace VertexBPMN.Domain.Entities;
public sealed record ApplicationDefinitionMetadata(string Id, string TenantId, string Key, string Name, int Version, string Content, string DiagnosticsJson = "[]");
