using System;

namespace VertexBPMN.Persistence.Models;

public class TaskEntity
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public ProcessInstance ProcessInstance { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
