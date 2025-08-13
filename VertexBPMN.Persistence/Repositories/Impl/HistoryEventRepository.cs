using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories.Impl;

/// <summary>
/// EF Core implementation of IHistoryEventRepository.
/// </summary>
public class HistoryEventRepository : IHistoryEventRepository
{
    private readonly BpmnDbContext _db;
    public HistoryEventRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default)
    {
        await _db.HistoryEvents.AddAsync(historyEvent, cancellationToken);
    }

    public async ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.HistoryEvents.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.HistoryEvents.Where(e => e.ProcessInstanceId == processInstanceId);
        await foreach (var evt in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return evt;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.HistoryEvents.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null) _db.HistoryEvents.Remove(entity);
    }
}
