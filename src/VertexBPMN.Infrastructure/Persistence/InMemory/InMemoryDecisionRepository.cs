using System.Collections.Concurrent;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Infrastructure.Persistence.InMemory;

/// <summary>
/// Simple in-memory implementation of IDecisionRepository for unit/integration tests
/// where persistence durability is not required. Thread-safe via concurrent collections.
/// </summary>
public class InMemoryDecisionRepository : IDecisionRepository
{
    private readonly ConcurrentDictionary<string, DecisionDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, List<DecisionInstance>> _instances = new();
    private readonly object _instanceLock = new();

    public ValueTask UpsertDefinitionAsync(DecisionDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var id = DecisionDefinition.BuildId(definition.Key, definition.TenantId);
        definition.Id = id;
        _definitions.AddOrUpdate(id, definition, (_, existing) =>
        {
            existing.Name = definition.Name;
            existing.DmnXml = definition.DmnXml;
            existing.DecisionTable = definition.DecisionTable;
            return existing;
        });
        return ValueTask.CompletedTask;
    }

    public ValueTask<DecisionDefinition?> GetDefinitionAsync(string key, string? tenantId = null, CancellationToken ct = default)
    {
        var id = DecisionDefinition.BuildId(key, tenantId);
        _definitions.TryGetValue(id, out var def);
        return ValueTask.FromResult(def);
    }

    public async IAsyncEnumerable<DecisionDefinition> ListDefinitionsAsync(string? key = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        IEnumerable<DecisionDefinition> query = _definitions.Values;
        if (!string.IsNullOrWhiteSpace(key))
            query = query.Where(d => d.Key == key);
        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(d => d.TenantId == tenantId);
        foreach (var d in query.OrderBy(d => d.Key))
        {
            ct.ThrowIfCancellationRequested();
            yield return d;
            await Task.Yield();
        }
    }

    public ValueTask AddInstanceAsync(DecisionInstance instance, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var key = DecisionDefinition.BuildId(instance.DecisionDefinitionKey, instance.TenantId);
        lock (_instanceLock)
        {
            if (!_instances.TryGetValue(key, out var list))
            {
                list = new List<DecisionInstance>();
                _instances[key] = list;
            }
            list.Add(instance);
            if (list.Count > 1000)
            {
                // trim oldest
                list.RemoveRange(0, list.Count - 1000);
            }
        }
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<DecisionInstance> ListInstancesAsync(string? decisionKey = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        List<DecisionInstance> snapshot = new();
        lock (_instanceLock)
        {
            foreach (var kv in _instances)
            {
                snapshot.AddRange(kv.Value);
            }
        }
        IEnumerable<DecisionInstance> query = snapshot;
        if (!string.IsNullOrWhiteSpace(decisionKey))
            query = query.Where(i => i.DecisionDefinitionKey == decisionKey);
        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(i => i.TenantId == tenantId);
        foreach (var inst in query.OrderByDescending(i => i.EvaluationTime))
        {
            ct.ThrowIfCancellationRequested();
            yield return inst;
            await Task.Yield();
        }
    }

    public ValueTask SaveChangesAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
}
