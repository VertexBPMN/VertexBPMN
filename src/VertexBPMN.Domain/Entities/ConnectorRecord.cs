namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Tenant-scoped connector definition. Credentials are referenced by id and are
/// deliberately never copied into this record.
/// </summary>
public sealed class ConnectorRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Endpoint { get; set; }
    public string? CredentialId { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
