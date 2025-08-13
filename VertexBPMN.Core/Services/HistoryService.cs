using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Core.Services;

/// <summary>
/// In-memory implementation of IHistoryService for development and testing.
/// </summary>
public class HistoryService : IHistoryService
{
    public async IAsyncEnumerable<HistoryEvent> ListHistoricTasksAsync(Guid? processInstanceId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await System.Threading.Tasks.Task.CompletedTask;
        yield break;
    }

    private readonly ConcurrentDictionary<Guid, HistoryEvent> _events = new();

    public async IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var evt in _events.Values)
        {
            if (evt.ProcessInstanceId == processInstanceId)
                yield return evt;
        }
        await System.Threading.Tasks.Task.CompletedTask;
    }

    public ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_events.TryGetValue(id, out var evt) ? evt : null);
}
