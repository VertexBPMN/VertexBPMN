namespace VertexBPMN.Core.Domain;

/// <summary>
/// Represents an incident (error, failure, etc.) during process execution.
/// </summary>
public class Incident
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? TenantId { get; set; }
    // TODO: Add resolution, status, etc.
}
