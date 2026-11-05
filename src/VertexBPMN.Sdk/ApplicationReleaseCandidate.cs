namespace VertexBPMN.Sdk;
public sealed record ApplicationReleaseCandidate(string Id, string TenantId, string ApplicationDefinitionId, string ApplicationRevisionId, string ManifestJson, bool IsReleased, long ReleasedAtTicks);
