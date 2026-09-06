using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public sealed class TaskIoSnapshotApiTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TaskIoSnapshotApiTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _factory = factory.WithSharedFixture(dbFixture);
        _client = _factory.CreateClient(output);
    }

    [Fact]
    public async Task IoSnapshots_StoredRedacted_AndListedNewestFirst()
    {
        var tenantId = $"tenant-{Guid.NewGuid():N}";
        const string elementId = "serviceTask_1";

        var processInstanceId = await SeedProcessAsync(tenantId);

        // Enable the global feature flag for this feature.
        await using (var flagScope = _factory.Services.CreateAsyncScope())
        {
            var db = flagScope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            var flag = await db.FeatureFlags.FindAsync(TaskIoSnapshotRecorder.FeatureFlagName);
            if (flag is null)
            {
                db.FeatureFlags.Add(new FeatureFlagRecord
                {
                    Name = TaskIoSnapshotRecorder.FeatureFlagName,
                    Enabled = true
                });
            }
            else
            {
                flag.Enabled = true;
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Record two snapshots through the real recorder (which redacts at write time).
        var recorder = _factory.Services.GetRequiredService<ITaskIoSnapshotRecorder>();
        await recorder.RecordAsync(
            processInstanceId, elementId, tenantId,
            new Dictionary<string, object> { ["user"] = "alice", ["apiToken"] = "secret-value-xyz" },
            new Dictionary<string, object> { ["ok"] = true },
            success: true, errorMessage: null, TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(20)); // ensure distinct timestamps
        await recorder.RecordAsync(
            processInstanceId, elementId, tenantId,
            new Dictionary<string, object> { ["user"] = "bob" }, output: null,
            success: false, errorMessage: "boom", TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            $"/api/process-instances/{processInstanceId}/tasks/{elementId}/io-snapshots?tenantId={tenantId}",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("secret-value-xyz", json, StringComparison.Ordinal);
        Assert.Contains("\"***\"", json, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(json);
        var array = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, array.Count);
        // Newest first: the second (failed) snapshot must come first.
        Assert.False(array[0].GetProperty("data").GetProperty("success").GetBoolean());
        Assert.True(array[1].GetProperty("data").GetProperty("success").GetBoolean());
    }

    private async Task<Guid> SeedProcessAsync(string tenantId)
    {
        await _factory.InitializeAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var deployment = new EngineDeployment
        {
            Id = Guid.NewGuid(),
            Name = $"IoSnapshotDeployment-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            TenantId = tenantId
        };
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = $"IoSnapshotProcess-{Guid.NewGuid():N}",
            Name = "Io Snapshot Process",
            Version = 1,
            TenantId = tenantId,
            DeploymentId = deployment.Id,
            TenantScope = tenantId,
            CreatedAt = DateTime.UtcNow,
            BpmnXml = ""
        };
        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            ProcessDefinitionId = definition.Id,
            TenantId = tenantId,
            State = "Running",
            ProcessId = definition.Key,
            InstanceId = Guid.NewGuid().ToString("N"),
            Status = ProcessInstanceStatus.Running,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            Variables = new Dictionary<string, object>()
        };

        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(definition);
        db.ProcessInstances.Add(instance);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return instance.Id;
    }
}
