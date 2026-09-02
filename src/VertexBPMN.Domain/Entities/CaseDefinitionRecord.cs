namespace VertexBPMN.Domain.Entities;

public sealed class CaseDefinitionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string TenantId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CmmnXml { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
