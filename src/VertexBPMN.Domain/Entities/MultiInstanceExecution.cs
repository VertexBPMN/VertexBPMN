namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Represents a multi-instance execution (e.g., for BPMN multi-instance activities).
/// </summary>
public class MultiInstanceExecution
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public int InstanceCount { get; set; }
    public int CompletedCount { get; set; }
    public bool IsSequential { get; set; }
    public int NextIndex { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public string? ElementVariable { get; set; }
    public string? CompletionCondition { get; set; }
    public string? OutputCollection { get; set; }
    public string State { get; set; } = "Active";
    public long Revision { get; set; }
}
