using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Integration.Applications;

public sealed class EndpointReconciliationTests
{
    [Fact]
    public async Task Worker_StartsAcceptedHandleUsingFrozenDefinitionAfterReleaseSwitch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();
        using var response = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", new { }, ct);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var operationId = body.GetProperty("operationId").GetString()!;
        await SwitchReleaseAsync(fixture, ct);
        await fixture.Services.GetRequiredService<JobExecutorService>().RunPollingIterationAsync(ct);
        var stored = await fixture.FindOperationAsync(operationId);
        Assert.NotNull(stored);
        Assert.Equal("Started", stored.Status);
        Assert.True(Guid.TryParse(stored.ProcessInstanceId, out var instanceId));
        await using var fresh = fixture.OpenDb();
        var instance = await fresh.ProcessInstances.AsNoTracking().SingleAsync(x => x.Id == instanceId, ct);
        Assert.Equal(fixture.ProcessId, instance.ProcessDefinitionId);
        Assert.Equal("tenant-a", instance.TenantId);
        using var result = JsonDocument.Parse(stored.ResultJson!);
        Assert.Equal("Started", result.RootElement.GetProperty("status").GetString());
        Assert.Equal(instanceId.ToString(), result.RootElement.GetProperty("processInstanceId").GetString());
        Assert.Equal(ProcessInstanceStatus.Running, instance.Status);
        Assert.True(await fresh.Tasks.AnyAsync(x => x.ProcessInstanceId == instanceId, ct));
    }

    private static async Task SwitchReleaseAsync(EndpointOperationFixture fixture, CancellationToken ct)
    {
        await using var db = fixture.OpenDb();
        var target = Guid.NewGuid();
        var priorDefinition = await db.ProcessDefinitions.SingleAsync(x => x.Id == fixture.ProcessId, ct);
        db.ProcessDefinitions.Add(new ProcessDefinition
        {
            Id = target, Key = fixture.ProcessKey + "-release2", Name = fixture.ProcessKey, Version = 2,
            TenantId = "tenant-a", DeploymentId = priorDefinition.DeploymentId,
            BpmnXml = priorDefinition.BpmnXml
        });
        await db.SaveChangesAsync(ct);
        var content = EndpointOperationContent.Json(fixture.ProcessKey + "-release2", target.ToString());
        var applications = VertexBPMN.Tests.Unit.Infrastructure.EndpointCheckpointStore.Service(db);
        Assert.True(await applications.UpdateAsync("tenant-a", fixture.Id,
            new(fixture.Key, fixture.Key, 1, content), ct));
        var releases = new VertexBPMN.Infrastructure.Persistence.Services.PersistentApplicationReleaseService(db);
        var operation = Guid.NewGuid().ToString("N");
        await releases.PrepareAsync(operation, "tenant-a", fixture.Id, 2,
            ReleaseManifest.Serialize(new ReleaseManifest
                { Key = fixture.Key, Revision = 2, Hash = ReleaseManifest.ComputeHash(content) }), ct);
        var active = await releases.ActivateAsync(operation, "tenant-a", fixture.Id, ct);
        Assert.Equal(target.ToString(), active.BoundProcessDefinitionId);
    }
}
