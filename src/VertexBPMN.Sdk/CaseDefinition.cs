namespace VertexBPMN.Sdk;
public sealed record CaseDefinition(string Id, string TenantId, string Key, string Name, string CmmnXml, DateTime CreatedAt, DateTime LastModified);
