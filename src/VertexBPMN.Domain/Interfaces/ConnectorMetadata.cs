namespace VertexBPMN.Domain.Interfaces;

public sealed record ConnectorMetadata(string Id, string TenantId, string Name, string Type, string? Description, string? Endpoint, string? CredentialId, string? TemplateId, bool Enabled, DateTime CreatedAt, DateTime LastModified);
