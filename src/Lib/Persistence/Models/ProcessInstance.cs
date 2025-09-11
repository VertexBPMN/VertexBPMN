using System;

namespace VertexBPMN.Persistence.Models;

public class ProcessInstance
{
    public Guid Id { get; set; }
    public Guid ProcessDefinitionId { get; set; }
    public ProcessDefinition ProcessDefinition { get; set; } = null!;
    public string? BusinessKey { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? TenantId { get; set; }
}
