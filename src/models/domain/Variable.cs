namespace VertexBPMN.Core.Domain;

/// <summary>
/// Represents a process or task variable.
/// </summary>
public class Variable
{
    public Guid Id { get; set; }
    public Guid ScopeId { get; set; } // ProcessInstanceId or TaskId
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? TenantId { get; set; }
    // TODO: Add serialization, scope type, etc.
}
