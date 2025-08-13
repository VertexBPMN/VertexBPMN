namespace VertexBPMN.Core.Domain;

/// <summary>
/// Represents a running or completed process instance.
/// </summary>
public class ProcessInstance
{
    public Guid Id { get; set; }
    public Guid ProcessDefinitionId { get; set; }
    public string? BusinessKey { get; set; }
    public string? TenantId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string State { get; set; } = string.Empty; // For visual debugger step-through
    // TODO: Add variables and other properties
}
