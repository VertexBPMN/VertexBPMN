using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Persistence.Repositories.Impl;

/// <summary>
/// EF Core implementation of IJobRepository.
/// </summary>
public class JobRepository : VertexBPMN.Core.Services.IJobRepository, VertexBPMN.Persistence.Repositories.IJobRepository
{
    private readonly BpmnDbContext _db;
    public JobRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        await _db.Jobs.AddAsync(job, cancellationToken);
    }

    public async ValueTask<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Jobs.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<Job> ListDueAsync(DateTime asOf, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.Jobs.Where(j => j.DueDate <= asOf);
        await foreach (var job in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return job;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Jobs.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null) _db.Jobs.Remove(entity);
    }
}
