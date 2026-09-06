using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Repositories;

public class PollingTriggerRepository(BpmnDbContext db) : IPollingTriggerRepository
{
    public async Task AddAsync(PollingTriggerRecord trigger, CancellationToken cancellationToken = default)
    {
        await db.PollingTriggers.AddAsync(trigger, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<PollingTriggerRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.PollingTriggers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PollingTriggerRecord>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
        => await db.PollingTriggers.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PollingTriggerRecord>> ListDueAsync(DateTime asOf, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await db.PollingTriggers.AsNoTracking()
            .Where(x => x.Enabled
                        && x.NextDueAt.HasValue
                        && x.NextDueAt <= asOf
                        && (!x.LockedUntil.HasValue || x.LockedUntil <= now))
            .OrderBy(x => x.NextDueAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryLeaseAsync(PollingTriggerRecord trigger, string workerId, DateTime lockedUntil, CancellationToken cancellationToken = default)
    {
        var updated = await db.PollingTriggers
            .Where(x => x.Id == trigger.Id
                        && x.Enabled
                        && (!x.LockedUntil.HasValue || x.LockedUntil <= DateTime.UtcNow))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LockOwner, workerId)
                .SetProperty(x => x.LockedUntil, lockedUntil), cancellationToken);
        return updated > 0;
    }

    public async Task UpdateAsync(PollingTriggerRecord trigger, CancellationToken cancellationToken = default)
    {
        db.ChangeTracker.Clear();
        db.PollingTriggers.Attach(trigger);
        db.Entry(trigger).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var updated = await db.PollingTriggers.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
        return updated > 0;
    }
}
