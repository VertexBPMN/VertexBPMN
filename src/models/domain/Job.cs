namespace VertexBPMN.Core.Domain;

/// <summary>
/// Represents a scheduled job (timer, async continuation, etc.).
/// </summary>
public class Job
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public int Retries { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TenantId { get; set; }
    // TODO: Add job handler, status, etc.
}
