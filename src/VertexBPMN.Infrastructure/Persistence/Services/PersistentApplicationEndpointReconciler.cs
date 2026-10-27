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
            .Where(x => x.Status == "Accepted" && x.BoundProcessDefinitionId != null)
            .OrderBy(x => x.CreatedAtTicks).Take(20).ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            if (!Guid.TryParse(candidate.BoundProcessDefinitionId, out var definitionId)) { continue; }
            var claimed = await db.ApplicationEndpointOperations
                .Where(x => x.Id == candidate.Id && x.Status == "Accepted")
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Running")
                    .SetProperty(x => x.UpdatedAtTicks, DateTime.UtcNow.Ticks), cancellationToken);
            if (claimed != 1) { continue; }
            // No retry/demotion after this claim: an exception can follow a committed effect.
            var instance = await runtime.StartProcessByIdAsync(definitionId,
                tenantId: candidate.TenantId, cancellationToken: cancellationToken);
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
