namespace VertexBPMN.Sdk;
public sealed record ApplicationRevision(string Id, string ApplicationDefinitionId, string TenantId, string ContentJson, string ContentHash, string DiagnosticsJson, int Version);
