using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain;

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
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");
    public string ProcessId { get; set; } = "";
    public ProcessInstanceStatus Status { get; set; } = ProcessInstanceStatus.Running;
    public List<string> ActiveTasks { get; set; } = new();
    public List<string> ActiveTokens { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
