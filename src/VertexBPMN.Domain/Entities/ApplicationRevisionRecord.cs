namespace VertexBPMN.Domain.Entities;
public sealed class ApplicationRevisionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string ApplicationDefinitionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public string ContentHash { get; set; } = string.Empty;
    public string DiagnosticsJson { get; set; } = "[]";
    public int Version { get; set; }
}
