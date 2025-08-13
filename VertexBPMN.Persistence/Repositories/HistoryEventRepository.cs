
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

public class HistoryEventRepository : IHistoryEventRepository
{
    private readonly BpmnDbContext _db;
    public HistoryEventRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default)
    {
        await _db.HistoryEvents.AddAsync(historyEvent, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.HistoryEvents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.HistoryEvents.AsNoTracking().Where(e => e.ProcessInstanceId == processInstanceId);
        await foreach (var evt in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return evt;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evt = await _db.HistoryEvents.FindAsync(new object[] { id }, cancellationToken);
        if (evt != null)
        {
            _db.HistoryEvents.Remove(evt);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
