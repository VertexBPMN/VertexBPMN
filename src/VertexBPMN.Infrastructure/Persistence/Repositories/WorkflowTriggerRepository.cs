using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Infrastructure.Persistence.Repositories;

public sealed class WorkflowTriggerRepository(BpmnDbContext db) : IWorkflowTriggerRepository
{
    public async Task<IReadOnlyList<WorkflowTrigger>> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default)
        => await db.WorkflowTriggers
            .AsNoTracking()
            .Where(trigger => tenantId == null || trigger.TenantId == tenantId)
            .OrderBy(trigger => trigger.Name)
            .ToListAsync(cancellationToken);

    public Task<WorkflowTrigger?> GetAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
        => db.WorkflowTriggers.SingleOrDefaultAsync(
            trigger => trigger.Id == id && (tenantId == null || trigger.TenantId == tenantId),
            cancellationToken);

    public async Task AddAsync(WorkflowTrigger trigger, CancellationToken cancellationToken = default)
    {
        db.WorkflowTriggers.Add(trigger);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var trigger = await GetAsync(id, tenantId, cancellationToken);
        if (trigger is null) return false;
        db.WorkflowTriggers.Remove(trigger);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task SaveAsync(WorkflowTrigger trigger, CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
