using System;

namespace VertexBPMN.Persistence.Models;

public class Job
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public ProcessInstance ProcessInstance { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Retries { get; set; }
    public DateTime DueDate { get; set; }
    public string? Payload { get; set; }
}
