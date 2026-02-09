namespace VertexBPMN.Domain.Entities;

public class ExecutionToken
{ 
    public ExecutionToken(Guid id, Guid processInstanceId, string currentNodeId, string nodeType, Dictionary<string, object> variables, DateTime createdAt, string? assignedWorker = null, DateTime? assignedAt = null, int retryCount = 0)
    {
        Id = id;
        ProcessInstanceId = processInstanceId;
        CurrentNodeId = currentNodeId;
        NodeType = nodeType;
        Variables = variables;
        CreatedAt = createdAt;
        AssignedWorker = assignedWorker;
        AssignedAt = assignedAt;
        RetryCount = retryCount;
    }

    public ExecutionToken(Guid id, Guid processInstanceId, string currentNodeId, string nodeType)
    {
        Id = id;
        ProcessInstanceId = processInstanceId;
        CurrentNodeId = currentNodeId;
        NodeType = nodeType;
        Variables = new Dictionary<string, object>();
        CreatedAt = DateTime.UtcNow;
        RetryCount = 0;
    }

    public ExecutionToken()
    {
    }

    // Optional factory if you still need string inputs
    public static ExecutionToken FromStrings(string id, string processInstanceId, string currentNodeId, string nodeType) =>
        new(Guid.Parse(id), Guid.Parse(processInstanceId), currentNodeId, nodeType);

    public Guid Id { get;  set; }
    public Guid ProcessInstanceId { get;  set; }
    public string CurrentNodeId { get;  set; } = null!;
    public string NodeType { get;  set; } = null!;
    public Dictionary<string, object> Variables { get;  set; } = new();
    public DateTime CreatedAt { get;  set; }
    public string? AssignedWorker { get;  set; }
    public DateTime? AssignedAt { get;  set; }
    public int RetryCount { get;  set; }
    public string? State { get;  set; }

    public void AssignWorker(string worker)
    {
        AssignedWorker = worker;
        AssignedAt = DateTime.UtcNow;
    }

    public void IncrementRetry() => RetryCount++;

    public void SetState(string state) => State = state;
}
