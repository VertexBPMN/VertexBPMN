using System;

namespace VertexBPMN.Persistence.Models;

public class ExecutionToken
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public ProcessInstance ProcessInstance { get; set; } = null!;
    public string ElementId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
