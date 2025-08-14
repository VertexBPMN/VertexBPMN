using System.Diagnostics;

namespace VertexBPMN.Core.Engine;

/// <summary>
/// Represents an execution token for advanced BPMN 2.0 flow control
/// </summary>
public record ExecutionToken
{
    public Guid Id;
    public Guid ProcessInstanceId;
    public string CurrentNodeId;
    public string NodeType;
    public IDictionary<string, object> Variables;
    public DateTime CreatedAt;
    public string? AssignedWorker = null;
    public DateTime? AssignedAt = null;
    public int RetryCount = 0;

    /// <summary>
    /// Represents an execution token for advanced BPMN 2.0 flow control
    /// </summary>
    public ExecutionToken(string Id, string ProcessInstanceId, string CurrentNodeId)
    {
        this.CurrentNodeId = CurrentNodeId;
        this.Id = Guid.Parse(Id);
        this.ProcessInstanceId = Guid.Parse(ProcessInstanceId);
    }

    public ExecutionToken(Guid Id, Guid ProcessInstanceId, string CurrentNodeId, string NodeType, IDictionary<string, object> Variables, DateTime CreatedAt, string? AssignedWorker = null, DateTime? AssignedAt = null, int RetryCount = 0)
    {
        this.Id = Id;
        this.ProcessInstanceId = ProcessInstanceId;
        this.CurrentNodeId = CurrentNodeId;
        this.NodeType = NodeType;
        this.Variables = Variables;
        this.CreatedAt = CreatedAt;
        this.AssignedWorker = AssignedWorker;
        this.AssignedAt = AssignedAt;
        this.RetryCount = RetryCount;
    }

}
