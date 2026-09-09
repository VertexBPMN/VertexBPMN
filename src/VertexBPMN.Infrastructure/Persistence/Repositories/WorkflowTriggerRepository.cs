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

    public Task<WorkflowTrigger?> GetByEndpointAsync(string path, string method, string? tenantId = null, CancellationToken cancellationToken = default)
        => db.WorkflowTriggers.SingleOrDefaultAsync(trigger =>
            trigger.Path == path && trigger.Method == method && (tenantId == null || trigger.TenantId == tenantId), cancellationToken);

    public Task<WorkflowTrigger?> GetBySourceElementAsync(string processDefinitionKey, string sourceElementId, string? tenantId = null, CancellationToken cancellationToken = default)
        => db.WorkflowTriggers.SingleOrDefaultAsync(trigger =>
            trigger.ProcessDefinitionKey == processDefinitionKey && trigger.SourceElementId == sourceElementId &&
            (tenantId == null || trigger.TenantId == tenantId), cancellationToken);

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

    public async Task<bool> TryReserveDeliveryAsync(Guid triggerId, string? tenantId, string deliveryId, CancellationToken cancellationToken = default)
    {
        var record = new RuntimeInboxMessage
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenantScope = tenantId ?? "$global",
            Operation = $"webhook:{triggerId:N}", IdempotencyKey = deliveryId, ReceivedAt = DateTime.UtcNow
        };
        db.RuntimeInbox.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 2067 or 1555 }
            || ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            db.Entry(record).State = EntityState.Detached;
            return false;
        }
    }
}
