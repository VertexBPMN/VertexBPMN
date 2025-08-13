
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

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
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<Variable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Variables.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async IAsyncEnumerable<Variable> ListByScopeAsync(Guid scopeId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.Variables.AsNoTracking().Where(v => v.ScopeId == scopeId);
        await foreach (var variable in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return variable;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var variable = await _db.Variables.FindAsync(new object[] { id }, cancellationToken);
        if (variable != null)
        {
            _db.Variables.Remove(variable);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
