namespace VertexBPMN.Infrastructure.Persistence;

public sealed class IdentityGroupRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? TenantId { get; set; }
}
