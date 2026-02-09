using System.Collections.Concurrent;
using System.Xml.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.InMemory;

public sealed class InMemoryRepositoryService : IRepositoryService
{
    private readonly ConcurrentDictionary<Guid, ProcessDefinition> _defs = new();
    private readonly ConcurrentDictionary<(string Key, string? Tenant), ProcessDefinition> _latest = new(TupleCmp.Instance);

    public ValueTask<ProcessDefinition> DeployAsync(string bpmnXml, string name, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        string processId = name;
        try
        {
            var doc = XDocument.Parse(bpmnXml);
            var ns = doc.Root?.Name.Namespace ?? "";
            var proc = doc.Descendants(ns + "process").FirstOrDefault();
            if (proc != null) processId = (string?)proc.Attribute("id") ?? name;
        }
        catch { /* ignore */ }

        var version = _latest.TryGetValue((processId, tenantId), out var prev) ? prev.Version + 1 : 1;
        var def = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = processId,
            Name = name,
            Version = version,
            BpmnXml = bpmnXml,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        _defs[def.Id] = def;
        _latest[(processId, tenantId)] = def;
        return ValueTask.FromResult(def);
    }

    public ValueTask<ProcessDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_defs.TryGetValue(id, out var d) ? d : null);

    public ValueTask<ProcessDefinition?> GetLatestByKeyAsync(string key, string? tenantId = null, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_latest.TryGetValue((key, tenantId), out var d) ? d : null);

    public async IAsyncEnumerable<ProcessDefinition> ListAsync(string? key = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var d in _defs.Values.Where(d => (key == null || d.Key == key) && (tenantId == null || d.TenantId == tenantId)))
        {
            yield return d;
            await Task.Yield();
        }
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_defs.TryRemove(id, out var removed))
        {
            var tuple = _latest.FirstOrDefault(k => k.Value.Id == removed.Id).Key;
            if (tuple.Key != null)
            {
                var rest = _defs.Values
                    .Where(x => x.Key == removed.Key && x.TenantId == removed.TenantId)
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefault();
                if (rest != null) _latest[(rest.Key, rest.TenantId)] = rest;
            }
        }
        return ValueTask.CompletedTask;
    }

    private sealed class TupleCmp : IEqualityComparer<(string, string?)>
    {
        public static readonly TupleCmp Instance = new();
        public bool Equals((string, string?) x, (string, string?) y)
            => string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string, string?) obj)
            => HashCode.Combine(obj.Item1.ToLowerInvariant(), obj.Item2?.ToLowerInvariant());
    }
}