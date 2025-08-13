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
/// EF Core implementation of IMultiInstanceExecutionRepository.
/// </summary>
public class MultiInstanceExecutionRepository : IMultiInstanceExecutionRepository
{
    private readonly BpmnDbContext _db;
    public MultiInstanceExecutionRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(MultiInstanceExecution execution, CancellationToken cancellationToken = default)
    {
        await _db.MultiInstanceExecutions.AddAsync(execution, cancellationToken);
    }

    public async ValueTask<MultiInstanceExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.MultiInstanceExecutions.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<MultiInstanceExecution> ListByProcessInstanceAsync(Guid processInstanceId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.MultiInstanceExecutions.Where(e => e.ProcessInstanceId == processInstanceId);
        await foreach (var exec in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return exec;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.MultiInstanceExecutions.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null) _db.MultiInstanceExecutions.Remove(entity);
    }
}
