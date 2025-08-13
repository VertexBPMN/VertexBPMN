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
/// EF Core implementation of ITaskRepository.
/// </summary>
public class TaskRepository : ITaskRepository
{
    private readonly BpmnDbContext _db;
    public TaskRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(VertexBPMN.Core.Domain.Task task, CancellationToken cancellationToken = default)
    {
        await _db.Tasks.AddAsync(task, cancellationToken);
    }

    public async ValueTask<VertexBPMN.Core.Domain.Task?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Tasks.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<VertexBPMN.Core.Domain.Task> ListAsync(Guid? processInstanceId = null, string? assignee = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.Tasks.AsQueryable();
        if (processInstanceId != null) query = query.Where(t => t.ProcessInstanceId == processInstanceId);
        if (assignee != null) query = query.Where(t => t.Assignee == assignee);
        await foreach (var task in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return task;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Tasks.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null) _db.Tasks.Remove(entity);
    }
}
