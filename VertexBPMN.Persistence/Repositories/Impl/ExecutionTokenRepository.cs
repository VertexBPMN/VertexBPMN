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
/// EF Core implementation of IExecutionTokenRepository.
/// </summary>
public class ExecutionTokenRepository : IExecutionTokenRepository
{
    private readonly BpmnDbContext _db;
    public ExecutionTokenRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(ExecutionToken token, CancellationToken cancellationToken = default)
    {
        await _db.ExecutionTokens.AddAsync(token, cancellationToken);
    }

    public async ValueTask<ExecutionToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.ExecutionTokens.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<ExecutionToken> ListByProcessInstanceAsync(Guid processInstanceId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.ExecutionTokens.Where(t => t.ProcessInstanceId == processInstanceId);
        await foreach (var token in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return token;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ExecutionTokens.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null) _db.ExecutionTokens.Remove(entity);
    }
}
