using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Integration.Applications;

public sealed class EndpointRecoveryTests
{
    [Fact]
    public async Task Recovery_ResolvesCrashedRunningHandleToStarted_WithoutDoubleStarting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();

        // A Running handle whose effect may or may not have been committed before a crash.
        var operationId = Guid.NewGuid().ToString("N");
        var idempotencyKey = "endpoint:" + operationId;
        await using (var db = fixture.OpenDb())
        {
            db.ApplicationEndpointOperations.Add(new ApplicationEndpointOperation
            {
                Id = Guid.NewGuid(), OperationId = operationId, TenantId = "tenant-a",
                ApplicationKey = fixture.Key, EndpointKey = "start", Status = "Running",
                ApplicationDefinitionId = fixture.Id, ApplicationRevisionId = "rev", ApplicationReleaseId = "rel",
                BoundProcessDefinitionId = fixture.ProcessId.ToString(),
                CreatedAtTicks = DateTime.UtcNow.Ticks, UpdatedAtTicks = DateTime.UtcNow.Ticks
            });
            await db.SaveChangesAsync(ct);
        }

        // Simulate the crash-after-start window: the previous worker already committed a real
        // process start under the same per-operation idempotency key, then died before writing Started.
        var runtime = fixture.Services.GetRequiredService<IRuntimeService>();
        var committed = await runtime.StartProcessByIdAsync(fixture.ProcessId, tenantId: "tenant-a",
            idempotencyKey: idempotencyKey, cancellationToken: ct);

        // Recovery reconciles the stuck Running handle without re-executing the already-performed start.
        var reconciler = fixture.Services.GetRequiredService<IApplicationEndpointReconciler>();
        await reconciler.ReconcileAcceptedAsync(ct);

        var stored = await fixture.FindOperationAsync(operationId);
        Assert.NotNull(stored);
        Assert.Equal("Started", stored.Status);
        Assert.Equal(committed.Id.ToString(), stored.ProcessInstanceId);

        // Exactly one process instance for this definition: recovery must not double-start.
        await using var fresh = fixture.OpenDb();
        Assert.Equal(1, await fresh.ProcessInstances.CountAsync(x => x.ProcessDefinitionId == fixture.ProcessId, ct));

        // A second recovery pass is idempotent: still one instance, same id, still Started.
        await reconciler.ReconcileAcceptedAsync(ct);
        Assert.Equal(1, await fresh.ProcessInstances.CountAsync(x => x.ProcessDefinitionId == fixture.ProcessId, ct));
        var again = await fixture.FindOperationAsync(operationId);
        Assert.Equal("Started", again!.Status);
        Assert.Equal(committed.Id.ToString(), again.ProcessInstanceId);
    }
}
