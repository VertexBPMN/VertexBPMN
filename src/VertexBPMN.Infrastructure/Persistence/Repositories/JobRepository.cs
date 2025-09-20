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
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var query = _db.Jobs.AsNoTracking().Where(j => j.DueDate <= asOf);
        await foreach (var job in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            if (job != null)
                yield return job;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _db.Jobs.FindAsync(new object[] { id }, cancellationToken);
        if (job != null)
        {
            _db.Jobs.Remove(job);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        _db.Jobs.Update(job);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
