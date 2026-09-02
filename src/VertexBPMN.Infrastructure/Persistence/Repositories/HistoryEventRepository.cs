using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Infrastructure.Persistence.Repositories;

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
        await foreach (var evt in ListAsync(processInstanceId, cancellationToken: cancellationToken))
            yield return evt;
    }

    public async IAsyncEnumerable<HistoryEvent> ListAsync(Guid? processInstanceId = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.HistoryEvents.AsNoTracking().AsQueryable();
        if (processInstanceId.HasValue)
            query = query.Where(e => e.ProcessInstanceId == processInstanceId.Value);
        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(e => e.TenantId == tenantId);

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
