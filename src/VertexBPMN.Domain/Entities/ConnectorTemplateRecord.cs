namespace VertexBPMN.Domain.Entities;

public sealed class ConnectorTemplateRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AppliesToJson { get; set; } = "[]";
    public string Runtime { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string PropertiesJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
