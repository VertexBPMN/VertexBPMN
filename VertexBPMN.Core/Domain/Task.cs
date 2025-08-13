namespace VertexBPMN.Core.Domain;

/// <summary>
/// Represents a user or service task instance.
/// </summary>
public class Task
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Assignee { get; set; }
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>
    /// Camunda formKey for user task forms (form-js, embedded forms, etc.)
    /// </summary>
    public string? FormKey { get; set; }

    /// <summary>
    /// Optional JSON schema for dynamic forms (form-js, Camunda 8, etc.)
    /// </summary>
    public string? FormSchema { get; set; }
    // TODO: Add candidate users/groups, etc.
}
