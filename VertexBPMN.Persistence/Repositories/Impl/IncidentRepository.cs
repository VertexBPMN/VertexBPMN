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
/// EF Core implementation of IIncidentRepository.
/// </summary>
public class IncidentRepository : IIncidentRepository
{
    private readonly BpmnDbContext _db;
    public IncidentRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        await _db.Incidents.AddAsync(incident, cancellationToken);
    }

    public async ValueTask<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Incidents.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<Incident> ListByProcessInstanceAsync(Guid processInstanceId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.Incidents.Where(i => i.ProcessInstanceId == processInstanceId);
        await foreach (var incident in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return incident;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Incidents.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null) _db.Incidents.Remove(entity);
    }
}
