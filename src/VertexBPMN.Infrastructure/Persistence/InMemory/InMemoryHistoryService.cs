using System.Collections.Concurrent;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.InMemory;

/// <summary>
/// In-memory implementation of IHistoryService for development and testing.
/// </summary>
public class InMemoryHistoryService : IHistoryService
{
    public async IAsyncEnumerable<HistoryEvent> ListHistoricTasksAsync(Guid? processInstanceId = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in ListAsync(processInstanceId, tenantId, cancellationToken))
        {
            if (evt.EventType.Contains("TASK", StringComparison.OrdinalIgnoreCase))
                yield return evt;
        }
    }

    private readonly ConcurrentDictionary<Guid, HistoryEvent> _events = new();

    public IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default)
        => ListAsync(processInstanceId, tenantId, cancellationToken);

    public IAsyncEnumerable<HistoryEvent> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default)
        => ListAsync(null, tenantId, cancellationToken);

    private async IAsyncEnumerable<HistoryEvent> ListAsync(Guid? processInstanceId, string? tenantId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var evt in _events.Values)
        {
            if ((!processInstanceId.HasValue || evt.ProcessInstanceId == processInstanceId.Value) &&
                (string.IsNullOrWhiteSpace(tenantId) || evt.TenantId == tenantId))
                yield return evt;
        }
        await Task.CompletedTask;
    }

    public ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_events.TryGetValue(id, out var evt) ? evt : null);
}