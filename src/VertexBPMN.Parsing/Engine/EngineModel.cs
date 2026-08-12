namespace VertexBPMN.Engine;

public sealed record EngineProcessDefinition(
    string Key,
    IReadOnlyDictionary<string, EngineFlowNode> Nodes,
    IReadOnlyList<EngineSequenceFlow> SequenceFlows,
    IReadOnlyList<string> StartEventIds,
    IReadOnlyDictionary<string, IReadOnlyList<EngineSequenceFlow>> Outgoing,
    IReadOnlyDictionary<string, IReadOnlyList<EngineSequenceFlow>> Incoming,
    DateTime DeployedAt,
    IReadOnlyList<string> Diagnostics
);

public sealed record EngineFlowNode(
    string Id,
    string Type,
    string? SubprocessId,
    bool IsGateway,
    bool IsEvent,
    bool IsTask,
    bool IsSubprocess,
    bool IsEndEvent,
    bool IsUserTask
);

public sealed record EngineSequenceFlow(
    string Id,
    string SourceId,
    string TargetId,
    bool IsDefault,
    string? ConditionExpression,
    int? Priority
);

public sealed record EngineToken(Guid Id,string NodeId,bool IsActive);

public sealed record EngineTaskInstance(Guid Id,string NodeId,string Name,DateTime CreatedAt,Dictionary<string,object?> Variables)
{
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed record EngineHistoryEvent(long Sequence,string EventType,string NodeId,DateTime Timestamp,string? Details=null);

public sealed record EngineProcessInstance(
    Guid Id,
    string ProcessKey,
    DateTime StartedAt,
    Dictionary<string, object?> Variables,
    List<EngineToken> Tokens,
    List<EngineTaskInstance> ActiveTasks,
    List<EngineHistoryEvent> History,
    bool Completed,
    DateTime? EndedAt
)
{
    public bool Completed { get; set; } = Completed;
    public DateTime? EndedAt { get; set; } = EndedAt;
    public IReadOnlyList<EngineTaskInstance> GetOpenTasks() => ActiveTasks.Where(t=>!t.Completed).ToList();
}

public sealed record EngineDeploymentResult(
    EngineProcessDefinition? ProcessDefinition,
    IReadOnlyList<string> MappingDiagnostics
);

public sealed record EngineStartResult(EngineProcessInstance Instance,IReadOnlyList<EngineTaskInstance> ActivatedTasks);

public sealed record EngineTaskCompletionResult(EngineProcessInstance Instance,EngineTaskInstance Task,IReadOnlyList<EngineTaskInstance> NewlyActivatedTasks,bool ProcessCompleted);
