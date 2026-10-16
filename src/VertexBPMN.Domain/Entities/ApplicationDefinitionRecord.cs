namespace VertexBPMN.Domain.Entities;
public sealed class ApplicationDefinitionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string TenantId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public int Version { get; set; }
}
