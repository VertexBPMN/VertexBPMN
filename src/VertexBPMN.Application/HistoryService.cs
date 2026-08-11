using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

/// <summary>
/// Persistent implementation of IHistoryService using IHistoryEventRepository.
/// </summary>
public class HistoryService : IHistoryService
{
    public async IAsyncEnumerable<HistoryEvent> ListHistoricTasksAsync(Guid? processInstanceId = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var historyEvent in _repo.ListAsync(processInstanceId, tenantId, cancellationToken))
        {
            if (historyEvent.EventType.Contains("TASK", StringComparison.OrdinalIgnoreCase))
                yield return historyEvent;
        }
    }
    private readonly IHistoryEventRepository _repo;
    public HistoryService(IHistoryEventRepository repo) => _repo = repo;

    public IAsyncEnumerable<HistoryEvent> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default)
        => _repo.ListAsync(tenantId: tenantId, cancellationToken: cancellationToken);

    public IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default)
        => _repo.ListAsync(processInstanceId, tenantId, cancellationToken);

    public ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);
}
