namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Persisted credential metadata and protected secret payload. The payload is never exposed by API DTOs.
/// </summary>
public sealed class CredentialRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SecretKeysJson { get; set; } = "[]";
    public string ProtectedValues { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
