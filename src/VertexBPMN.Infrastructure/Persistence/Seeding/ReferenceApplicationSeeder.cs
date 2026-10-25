using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.Lc06;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Infrastructure.Persistence.Seeding;

/// <summary>
/// Loads the LC06 reference application as a RELEASED revision through the platform's own
/// application release pipeline (create the application/revision, prepare a release candidate,
/// release it), so the pinned revision is exactly what the API and the Portal BFF serve via
/// <c>GET /released-revision</c> / <c>GET /api/bff/applications/{id}/released</c>. It ensures the
/// reference process the application starts is deployed first, and is idempotent: if the
/// application key already exists it is left untouched.
/// </summary>
public static class ReferenceApplicationSeeder
{
    /// <summary>
    /// Seeds the reference application as a released revision. Returns the application
    /// definition id, or <c>null</c> when the application was already present.
    /// </summary>
    public static async Task<string?> SeedReleasedAsync(BpmnDbContext db, IAuditLogService auditLogService, ApplicationAccessContext access, CancellationToken ct = default)
    {
        const string tenantId = Lc06ReferenceApp.TenantId;
        var exists = await db.ApplicationDefinitions.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Key == Lc06ReferenceApp.Key, ct).ConfigureAwait(false);
        if (exists)
        {
            return null;
        }

        await EnsureReferenceProcessAsync(db, tenantId, ct).ConfigureAwait(false);

        var service = new PersistentApplicationDefinitionService(db, auditLogService, access: access);
        var application = await service.CreateAsync(tenantId, new(Lc06ReferenceApp.Key, Lc06ReferenceApp.Name, 0, Lc06ReferenceApp.ContentJson), ct).ConfigureAwait(false);
        var candidate = await service.CreateReleaseCandidateAsync(tenantId, application.Id, 1, ct).ConfigureAwait(false);
        await service.ReleaseAsync(tenantId, candidate.Id, ct).ConfigureAwait(false);
        return application.Id;
    }

    private static async Task EnsureReferenceProcessAsync(BpmnDbContext db, string tenantId, CancellationToken ct)
    {
        var processId = Guid.Parse(Lc06ReferenceApp.ProcessId);
        if (await db.ProcessDefinitions.AsNoTracking().AnyAsync(x => x.Id == processId, ct).ConfigureAwait(false))
        {
            return;
        }

        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "lc06-reference-deployment", TenantId = tenantId, CreatedAt = DateTime.UtcNow };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(new ProcessDefinition
        {
            Id = processId,
            Key = "lc06-reference-flow",
            Name = "LC06 Reference Flow",
            Version = 1,
            BpmnXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" />",
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            DeploymentId = deployment.Id,
            TenantScope = "$global"
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
