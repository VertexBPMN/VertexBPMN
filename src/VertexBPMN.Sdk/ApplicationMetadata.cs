namespace VertexBPMN.Sdk;
public sealed record ApplicationMetadata(string Id, string TenantId, string Key, string Name, int Version, string Content, string DiagnosticsJson = "[]");
