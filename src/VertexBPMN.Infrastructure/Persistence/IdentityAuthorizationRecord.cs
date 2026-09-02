namespace VertexBPMN.Infrastructure.Persistence;

public sealed class IdentityAuthorizationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string UserId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Permissions { get; set; } = string.Empty;
    public string? TenantId { get; set; }
}
