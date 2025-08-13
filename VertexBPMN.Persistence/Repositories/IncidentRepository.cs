
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

public class IncidentRepository : IIncidentRepository
{
    private readonly BpmnDbContext _db;
    public IncidentRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        await _db.Incidents.AddAsync(incident, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Incidents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async IAsyncEnumerable<Incident> ListByProcessInstanceAsync(Guid processInstanceId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.Incidents.AsNoTracking().Where(i => i.ProcessInstanceId == processInstanceId);
        await foreach (var incident in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return incident;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents.FindAsync(new object[] { id }, cancellationToken);
        if (incident != null)
        {
            _db.Incidents.Remove(incident);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
