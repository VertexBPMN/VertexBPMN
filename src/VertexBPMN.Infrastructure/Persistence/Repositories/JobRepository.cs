using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Repositories;

public class JobRepository : IJobRepository
{
    private readonly BpmnDbContext _db;
    public JobRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        await _db.Jobs.AddAsync(job, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Jobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async IAsyncEnumerable<Job> ListDueAsync(DateTime asOf, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.Jobs.AsNoTracking().Where(j =>
            j.DueDate <= asOf
            && j.State == "Scheduled"
            && (!j.LockedUntil.HasValue || j.LockedUntil <= now));
        // Materialize before yielding. Keeping the relational reader open while a
        // job executes blocks concurrent commands on shared SQLite connections and
        // unnecessarily extends server-side cursors on production providers.
        var dueJobs = await query.OrderBy(job => job.DueDate).ToListAsync(cancellationToken);
        foreach (var job in dueJobs)
            yield return job;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        var job = await _db.Jobs.FindAsync(new object[] { id }, cancellationToken);
        if (job != null)
        {
            _db.Jobs.Remove(job);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        var originalRevision = job.Revision;
        job.Revision++;
        _db.Jobs.Attach(job);
        _db.Entry(job).State = EntityState.Modified;
        _db.Entry(job).Property(item => item.Revision).OriginalValue = originalRevision;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<bool> TryLeaseAsync(Job job, string workerId, DateTime lockedUntil, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var updated = await _db.Jobs
            .Where(item => item.Id == job.Id
                           && item.Revision == job.Revision
                           && item.State == "Scheduled"
                           && (!item.LockedUntil.HasValue || item.LockedUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, "Executing")
                .SetProperty(item => item.LockOwner, workerId)
                .SetProperty(item => item.LockedUntil, lockedUntil)
                .SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
        if (updated == 0) return false;
        job.State = "Executing";
        job.LockOwner = workerId;
        job.LockedUntil = lockedUntil;
        job.Revision++;
        return true;
    }
}
