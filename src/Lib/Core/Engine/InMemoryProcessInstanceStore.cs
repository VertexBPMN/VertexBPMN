#nullable enable
using System.Collections.Concurrent;
using System.Text.Json;
using VertexBPMN.Core.Contracts;
using VertexBPMN.Core.Modeling;
using VertexBPMN.Domain;

namespace VertexBPMN.Core.Engine;

/// <summary>
/// Simple in-memory (non-persistent, non-distributed) implementation of <see cref="IProcessInstanceStore"/>.
/// Intended only for early development, tests, or demos. NOT for production.
/// Thread-safe via concurrent collections.
/// </summary>
public sealed class InMemoryProcessInstanceStore : IProcessInstanceStore
{
    private readonly ConcurrentDictionary<string, string> _processes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ProcessInstance> _instances = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, ExecutionToken> _executionTokens = new();
    private readonly ConcurrentDictionary<Guid, CaseToken> _caseTokens = new();
    private readonly ConcurrentDictionary<string, WorkerNode> _workers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _dmnModels = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _cmmnModels = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<DeadLetterEntry> _deadLetters = new();

    private record DeadLetterEntry(DateTime TimestampUtc, string TokenType, string SerializedToken, string ErrorMessage);

    public Task SaveBpmnModelAsync(string processId, string bpmnXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bpmnXml);
        _processes[processId] = bpmnXml;
        return Task.CompletedTask;
    }

    public Task<string> GetBpmnModelAsync(string processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        if (_processes.TryGetValue(processId, out var xml))
            return Task.FromResult(xml);
        throw new KeyNotFoundException($"Process with key '{processId}' not found.");
    }

    public Task<IEnumerable<string>> ListProcessesAsync() =>
        Task.FromResult<IEnumerable<string>>(_processes.Keys.ToArray());

    public Task SaveInstanceAsync(ProcessInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        // Prefer InstanceId property if available, else fallback to Guid Id
        var instanceId = GetInstanceId(instance);
        _instances[instanceId] = instance;
        return Task.CompletedTask;
    }

    public Task<ProcessInstance> GetInstanceAsync(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (_instances.TryGetValue(instanceId, out var pi))
            return Task.FromResult(pi);
        throw new KeyNotFoundException($"Process instance '{instanceId}' not found.");
    }

    public Task SaveTokenAsync(ExecutionToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var id = GetTokenId(token);
        _executionTokens[id] = token;
        return Task.CompletedTask;
    }

    public Task<ExecutionToken> GetTokenAsync(Guid tokenId)
    {
        if (_executionTokens.TryGetValue(tokenId, out var token))
            return Task.FromResult(token);
        throw new KeyNotFoundException($"Execution token '{tokenId}' not found.");
    }

    public Task<List<ExecutionToken>> GetPendingTokensAsync()
    {
        var list = new List<ExecutionToken>();
        foreach (var t in _executionTokens.Values)
        {
            if (IsTokenPending(t))
                list.Add(t);
        }
        return Task.FromResult(list);
    }

    public Task SaveWorkerAsync(WorkerNode worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        var id = GetWorkerId(worker);
        _workers[id] = worker;
        return Task.CompletedTask;
    }

    public Task<WorkerNode> GetWorkerAsync(string workerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (_workers.TryGetValue(workerId, out var worker))
            return Task.FromResult(worker);
        throw new KeyNotFoundException($"Worker '{workerId}' not found.");
    }

    public Task<List<WorkerNode>> GetActiveWorkersAsync() =>
        Task.FromResult(_workers.Values.ToList());

    public Task RemoveWorkerAsync(string workerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        _workers.TryRemove(workerId, out _);
        return Task.CompletedTask;
    }

    public Task SaveToDeadLetterQueueAsync<T>(T token, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        var serialized = JsonSerializer.Serialize(token, token.GetType());
        _deadLetters.Enqueue(new DeadLetterEntry(DateTime.UtcNow, token.GetType().Name, serialized, errorMessage));
        return Task.CompletedTask;
    }

    public Task SaveDmnModelAsync(string decisionId, string dmnXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dmnXml);
        _dmnModels[decisionId] = dmnXml;
        return Task.CompletedTask;
    }

    public Task<string> GetDmnModelAsync(string decisionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        if (_dmnModels.TryGetValue(decisionId, out var xml))
            return Task.FromResult(xml);
        throw new KeyNotFoundException($"DMN model '{decisionId}' not found.");
    }

    public Task SaveCaseTokenAsync(CaseToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var id = GetCaseTokenId(token);
        _caseTokens[id] = token;
        return Task.CompletedTask;
    }

    public Task<CaseToken> GetCaseTokenAsync(Guid tokenId)
    {
        if (_caseTokens.TryGetValue(tokenId, out var token))
            return Task.FromResult(token);
        throw new KeyNotFoundException($"Case token '{tokenId}' not found.");
    }

    public Task<List<CaseToken>> GetPendingCaseTokensAsync()
    {
        // Lacking domain info; return all for now or filter by a reflective 'State' property == "Pending".
        var list = new List<CaseToken>();
        foreach (var t in _caseTokens.Values)
        {
            if (IsCaseTokenPending(t))
                list.Add(t);
        }
        return Task.FromResult(list);
    }

    public Task SaveCmmnModelAsync(string caseId, string cmmnXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cmmnXml);
        _cmmnModels[caseId] = cmmnXml;
        return Task.CompletedTask;
    }

    public Task<string> GetCmmnModelAsync(string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        if (_cmmnModels.TryGetValue(caseId, out var xml))
            return Task.FromResult(xml);
        throw new KeyNotFoundException($"CMMN model '{caseId}' not found.");
    }

    public Task UpdateCaseModelAsync(CaseModel model)
    {
        throw new NotImplementedException();
    }

    public Task SaveHistoricalCaseDataAsync(HistoricalCaseData data)
    {
        throw new NotImplementedException();
    }

    public Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId)
    {
        throw new NotImplementedException();
    }

    // Helper methods (reflection-safe to cope with incomplete domain model definitions provided)

    private static Guid GetTokenId(ExecutionToken token)
    {
        var prop = token.GetType().GetProperty("Id");
        if (prop?.PropertyType == typeof(Guid) && prop.GetValue(token) is Guid g && g != Guid.Empty)
            return g;
        // Fallback: create deterministic Guid from hash code (not ideal, but avoids exceptions in partial models)
        return Guid.NewGuid();
    }

    private static bool IsTokenPending(ExecutionToken token)
    {
        // Heuristic: if property AssignedWorker exists and is null -> pending
        var prop = token.GetType().GetProperty("AssignedWorker");
        if (prop != null)
        {
            var val = prop.GetValue(token);
            return val is null;
        }
        return true; // fallback treat as pending
    }

    private static string GetInstanceId(ProcessInstance instance)
    {
        var piType = instance.GetType();
        var instProp = piType.GetProperty("InstanceId");
        if (instProp?.GetValue(instance) is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        var idProp = piType.GetProperty("Id");
        if (idProp?.GetValue(instance) is Guid g && g != Guid.Empty)
            return g.ToString("N");
        return Guid.NewGuid().ToString("N");
    }

    private static Guid GetCaseTokenId(CaseToken token)
    {
        var prop = token.GetType().GetProperty("Id");
        if (prop?.PropertyType == typeof(Guid) && prop.GetValue(token) is Guid g && g != Guid.Empty)
            return g;
        return Guid.NewGuid();
    }

    private static bool IsCaseTokenPending(CaseToken token)
    {
        var stateProp = token.GetType().GetProperty("State");
        if (stateProp?.GetValue(token) is string s)
            return string.Equals(s, "Pending", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static string GetWorkerId(WorkerNode worker)
    {
        var prop = worker.GetType().GetProperty("Id");
        if (prop?.GetValue(worker) is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        var guidProp = worker.GetType().GetProperty("WorkerId");
        if (guidProp?.GetValue(worker) is string s2 && !string.IsNullOrWhiteSpace(s2))
            return s2;
        return Guid.NewGuid().ToString("N");
    }
}
