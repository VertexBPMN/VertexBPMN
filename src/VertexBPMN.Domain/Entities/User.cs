namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Application user aggregate (simplified). Stored in persistence for task assignments & authorization checks.
/// </summary>
public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
