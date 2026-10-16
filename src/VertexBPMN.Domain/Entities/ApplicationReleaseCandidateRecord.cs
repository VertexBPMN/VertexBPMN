namespace VertexBPMN.Domain.Entities;

public sealed class ApplicationReleaseCandidateRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string TenantId { get; set; } = string.Empty;
    public string ApplicationDefinitionId { get; set; } = string.Empty;
    public string ApplicationRevisionId { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = "{}";
    public bool IsReleased { get; set; }
    public long ReleasedAtTicks { get; set; }
}
