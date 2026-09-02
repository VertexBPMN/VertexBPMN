namespace VertexBPMN.Infrastructure.Persistence;

public sealed class IdentityGroupMembershipRecord
{
    public string GroupId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? TenantId { get; set; }
}
