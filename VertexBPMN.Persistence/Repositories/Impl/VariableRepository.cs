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
/// EF Core implementation of IVariableRepository.
/// </summary>
public class VariableRepository : IVariableRepository
{
    private readonly BpmnDbContext _db;
    public VariableRepository(BpmnDbContext db) => _db = db;

    public async ValueTask UpsertAsync(Variable variable, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Variables.FindAsync(new object[] { variable.Id }, cancellationToken);
        if (existing == null)
            await _db.Variables.AddAsync(variable, cancellationToken);
        else
            _db.Entry(existing).CurrentValues.SetValues(variable);
    }

    public async ValueTask<Variable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Variables.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<Variable> ListByScopeAsync(Guid scopeId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.Variables.Where(v => v.ScopeId == scopeId);
        await foreach (var variable in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return variable;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Variables.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null) _db.Variables.Remove(entity);
    }
}
