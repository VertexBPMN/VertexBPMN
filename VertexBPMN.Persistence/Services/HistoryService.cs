using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Services;
using VertexBPMN.Persistence.Repositories;

namespace VertexBPMN.Persistence.Services;

/// <summary>
/// Persistent implementation of IHistoryService using IHistoryEventRepository.
/// </summary>
public class HistoryService : IHistoryService
{
    public async IAsyncEnumerable<HistoryEvent> ListHistoricTasksAsync(Guid? processInstanceId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await System.Threading.Tasks.Task.CompletedTask;
        yield break;
    }
    private readonly IHistoryEventRepository _repo;
    public HistoryService(IHistoryEventRepository repo) => _repo = repo;

    public IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
        => _repo.ListByProcessInstanceAsync(processInstanceId, cancellationToken);

    public ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);
}
