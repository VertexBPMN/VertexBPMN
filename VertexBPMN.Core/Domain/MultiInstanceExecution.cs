namespace VertexBPMN.Core.Domain;

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
    // TODO: Add more properties as needed (variables, token references, etc.)
}
