namespace VertexBPMN.Domain.Entities;

public sealed class FormDefinitionRevisionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string FormDefinitionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "{}";
    public string SchemaHash { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
}
