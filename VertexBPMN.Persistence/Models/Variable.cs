using System;

namespace VertexBPMN.Persistence.Models;

public class Variable
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public ProcessInstance ProcessInstance { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
