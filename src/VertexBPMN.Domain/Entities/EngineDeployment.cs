namespace VertexBPMN.Domain.Entities;

public class EngineDeployment
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? TenantId { get; set; }
}
