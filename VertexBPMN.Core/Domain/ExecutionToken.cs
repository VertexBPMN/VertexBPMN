namespace VertexBPMN.Core.Domain;

/// <summary>
/// Represents a token in the BPMN execution graph.
/// </summary>
public class ExecutionToken
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    // TODO: Add parent/child token relationships, state, etc.
}
