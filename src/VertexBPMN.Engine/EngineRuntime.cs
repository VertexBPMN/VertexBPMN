namespace VertexBPMN.Engine;

/// <summary>
/// Advanced in-memory runtime for executing mapped process definitions (Phase K enhanced).
/// Supports:
///  - Deployment registry
///  - Process start (tokens on start events)
///  - Token propagation across sequence flows (exclusive semantics when multiple outgoing flows -> first matching condition)
///  - User task activation & completion
///  - Automatic end detection
///  - History event capture
/// Thread-safe via internal lock.
/// </summary>
public sealed class EngineRuntime
{
    private readonly Dictionary<string, EngineProcessDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, EngineProcessInstance> _instances = new();
    private readonly object _lock = new();
    private long _historySeq = 0;

    public IReadOnlyCollection<EngineProcessDefinition> Definitions => _definitions.Values;

    public EngineDeploymentResult Deploy(EngineProcessDefinition definition)
    {
        lock (_lock)
        {
            _definitions[definition.Key] = definition;
        }
        return new EngineDeploymentResult(definition, definition.Diagnostics);
    }

    public EngineStartResult Start(string processKey, IDictionary<string, object?>? variables = null)
    {
        EngineProcessDefinition def;
        lock (_lock)
        {
            if (!_definitions.TryGetValue(processKey, out def!))
                throw new InvalidOperationException($"Process not deployed: {processKey}");
        }
        var inst = new EngineProcessInstance(Guid.NewGuid(), processKey, DateTime.UtcNow,
            new Dictionary<string, object?>(variables ?? new Dictionary<string, object?>(), StringComparer.Ordinal),
            new List<EngineToken>(), new List<EngineTaskInstance>(), new List<EngineHistoryEvent>(), false,null);

        // Create tokens at start events
        foreach (var startId in def.StartEventIds)
        {
            var token = new EngineToken(Guid.NewGuid(), startId, true);
            inst.Tokens.Add(token);
            AddHistory(inst,"StartEvent", startId);
            Propagate(def, inst, token);
        }
        lock (_lock)
        {
            _instances[inst.Id] = inst;
        }
        return new EngineStartResult(inst, inst.GetOpenTasks());
    }

    public EngineTaskCompletionResult CompleteUserTask(Guid instanceId, Guid taskId, IDictionary<string, object?>? variables = null)
    {
        EngineProcessInstance inst; EngineProcessDefinition def;
        lock (_lock)
        {
            inst = GetInstanceInternal(instanceId) ?? throw new InvalidOperationException($"Instance not found: {instanceId}");
            def = _definitions[inst.ProcessKey];
        }
        var task = inst.ActiveTasks.FirstOrDefault(t=>t.Id==taskId) ?? throw new InvalidOperationException($"Task not found: {taskId}");
        if (task.Completed) throw new InvalidOperationException("Task already completed");
        if (variables != null)
        {
            foreach (var kv in variables) inst.Variables[kv.Key] = kv.Value;
        }
        task.Completed = true; task.CompletedAt = DateTime.UtcNow;
        AddHistory(inst,"TaskCompleted", task.NodeId);
        // Reactivate a token at the user task node for continuation
        var token = new EngineToken(Guid.NewGuid(), task.NodeId, true);
        inst.Tokens.Add(token);
        Propagate(def, inst, token);
        FinalizeIfEnded(def, inst);
        return new EngineTaskCompletionResult(inst, task, inst.GetOpenTasks(), inst.Completed);
    }

    public EngineProcessInstance? GetInstance(Guid id)
    {
        lock (_lock) return GetInstanceInternal(id);
    }

    private EngineProcessInstance? GetInstanceInternal(Guid id) => _instances.TryGetValue(id, out var i) ? i : null;

    private void Propagate(EngineProcessDefinition def, EngineProcessInstance inst, EngineToken token)
    {
        // If token already inactive skip
        if (!token.IsActive) return;
        if (!def.Outgoing.TryGetValue(token.NodeId, out var outgoing))
        {
            if (def.Nodes.TryGetValue(token.NodeId, out var node) && node.IsEndEvent)
            {
                DeactivateToken(inst, token.Id);
                AddHistory(inst,"EndEvent", token.NodeId);
                FinalizeIfEnded(def, inst);
            }
            return;
        }
        EngineSequenceFlow? selected = null;
        foreach (var flow in outgoing)
        {
            if (flow.ConditionExpression == null || EvaluateCondition(flow.ConditionExpression, inst.Variables)) { selected = flow; break; }
        }
        if (selected == null)
        {
            // dead end: deactivate token to avoid blocking completion
            DeactivateToken(inst, token.Id);
            return;
        }
        // consume current token
        DeactivateToken(inst, token.Id);
        AddHistory(inst, "SequenceFlow", selected.Id);
        var nextNodeId = selected.TargetId;
        if (!def.Nodes.TryGetValue(nextNodeId, out var nextNode)) return;
        if (nextNode.IsUserTask)
        {
            var task = new EngineTaskInstance(Guid.NewGuid(), nextNode.Id, nextNode.Id, DateTime.UtcNow, new Dictionary<string, object?>());
            inst.ActiveTasks.Add(task);
            AddHistory(inst, "TaskCreated", nextNode.Id);
        }
        else if (nextNode.IsEndEvent)
        {
            var endToken = new EngineToken(Guid.NewGuid(), nextNode.Id, true);
            inst.Tokens.Add(endToken);
            DeactivateToken(inst, endToken.Id); // immediate consumption at end
            AddHistory(inst,"EndEvent", nextNode.Id);
            FinalizeIfEnded(def, inst);
        }
        else
        {
            var newToken = new EngineToken(Guid.NewGuid(), nextNode.Id, true);
            inst.Tokens.Add(newToken);
            Propagate(def, inst, newToken);
        }
    }

    private void DeactivateToken(EngineProcessInstance inst, Guid tokenId)
    {
        for (int i=0;i<inst.Tokens.Count;i++)
        {
            if (inst.Tokens[i].Id == tokenId)
            {
                var t = inst.Tokens[i];
                if (t.IsActive)
                    inst.Tokens[i] = t with { IsActive = false };
                return;
            }
        }
    }

    private void FinalizeIfEnded(EngineProcessDefinition def, EngineProcessInstance inst)
    {
        if (inst.Completed) return;
        bool activeNonEndTokens = inst.Tokens.Any(t => t.IsActive && def.Nodes.TryGetValue(t.NodeId, out var n) && !n.IsEndEvent && !n.IsUserTask);
        bool openTasks = inst.ActiveTasks.Any(t=>!t.Completed);
        if (!activeNonEndTokens && !openTasks)
        {
            inst.Completed = true;
            inst.EndedAt = DateTime.UtcNow;
            AddHistory(inst,"ProcessCompleted", def.Key);
        }
    }

    private void AddHistory(EngineProcessInstance inst,string type,string nodeId,string? details=null)
    {
        var seq = Interlocked.Increment(ref _historySeq);
        inst.History.Add(new EngineHistoryEvent(seq,type,nodeId,DateTime.UtcNow,details));
    }

    private bool EvaluateCondition(string expr, IDictionary<string, object?> vars)
    {
        // Extremely naive: treat empty or whitespace as true, otherwise if it contains a variable reference of form ${var} ensure var exists and truthy.
        if (string.IsNullOrWhiteSpace(expr)) return true;
        if (expr.Contains("${") && expr.Contains("}"))
        {
            var start = expr.IndexOf("${", StringComparison.Ordinal);
            var end = expr.IndexOf('}', start+2);
            if (start>=0 && end>start)
            {
                var varName = expr.Substring(start+2, end-start-2).Trim();
                if (vars.TryGetValue(varName, out var value) && value is bool b) return b;
                if (value is string s) return !string.IsNullOrWhiteSpace(s);
                if (value is int i) return i!=0;
                return value != null;
            }
        }
        // Fallback: treat non-empty expression as true
        return true;
    }
}
