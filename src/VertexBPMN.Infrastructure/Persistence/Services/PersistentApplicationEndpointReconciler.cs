using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

/// <summary>Starts only Accepted handles. Running/uncertain starts require MS-2b reconciliation.</summary>
public sealed class PersistentApplicationEndpointReconciler(BpmnDbContext db, IRuntimeService runtime)
    : IApplicationEndpointReconciler
{
    public async Task ReconcileAcceptedAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await db.ApplicationEndpointOperations.AsNoTracking()
            .Where(x => (x.Status == "Accepted" || x.Status == "Running") && x.BoundProcessDefinitionId != null)
            .OrderBy(x => x.CreatedAtTicks).Take(20).ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            if (!Guid.TryParse(candidate.BoundProcessDefinitionId, out var definitionId)) { continue; }
            if (candidate.Status == "Accepted")
            {
                var claimed = await db.ApplicationEndpointOperations
                    .Where(x => x.Id == candidate.Id && x.Status == "Accepted")
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Running")
                        .SetProperty(x => x.UpdatedAtTicks, DateTime.UtcNow.Ticks), cancellationToken);
                if (claimed != 1) { continue; }
            }
            // No blanket 'Running -> Failed' demotion and no unconditional re-execution: a Running
            // handle means the start MIGHT already have been committed before a crash. A stable,
            // per-operation idempotency key makes the start exactly-once at the engine level - a
            // recovery retry returns the already-committed instance instead of starting a second
            // process (see IProcessExecutionRuntime inbox deduplication). The crash window between
            // the committed start and the Started write is therefore closed without double effects.
            var instance = await runtime.StartProcessByIdAsync(definitionId,
                tenantId: candidate.TenantId, idempotencyKey: "endpoint:" + candidate.OperationId,
                cancellationToken: cancellationToken);
            candidate.Status = "Started";
            candidate.ProcessInstanceId = instance.Id.ToString();
            candidate.ResultJson = JsonSerializer.Serialize(new
                { processInstanceId = instance.Id.ToString(), status = "Started" });
            candidate.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            db.ApplicationEndpointOperations.Update(candidate);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
