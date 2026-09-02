namespace VertexBPMN.Domain.Entities.Debugging;

// Data Models
public class DebugSession
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DebugStatus Status { get; set; }
    public ExecutionState ExecutionState { get; set; }
    public DebugOptions Options { get; set; } = new();
    public string CurrentActivityId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public List<CallStackFrame> CallStack { get; set; } = new();
}