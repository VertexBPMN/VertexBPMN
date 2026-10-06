namespace VertexBPMN.Sdk;

public sealed record CredentialMetadata(string Id, string TenantId, string Name, string Type, string? Description, IReadOnlyList<string> SecretKeys, DateTime CreatedAt, DateTime LastModified, DateTime? LastUsedAt);
