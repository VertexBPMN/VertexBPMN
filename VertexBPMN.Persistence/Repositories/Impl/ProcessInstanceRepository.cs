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
/// EF Core implementation of IProcessInstanceRepository.
/// </summary>
public class ProcessInstanceRepository : IProcessInstanceRepository
{
    private readonly BpmnDbContext _db;
    public ProcessInstanceRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(ProcessInstance instance, CancellationToken cancellationToken = default)
    {
    await _db.ProcessInstances.AddAsync(instance, cancellationToken);
    await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<ProcessInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.ProcessInstances.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<ProcessInstance> ListAsync(Guid? processDefinitionId = null, string? tenantId = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.ProcessInstances.AsQueryable();
        if (processDefinitionId != null) query = query.Where(i => i.ProcessDefinitionId == processDefinitionId);
        if (tenantId != null) query = query.Where(i => i.TenantId == tenantId);
        await foreach (var inst in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return inst;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ProcessInstances.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null) _db.ProcessInstances.Remove(entity);
    }
}
