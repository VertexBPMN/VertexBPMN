using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class TaskIoSnapshotRecorderTests
{
    private static async Task<BpmnDbContext> NewDbAsync(bool flagEnabled)
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new BpmnDbContext(options);
        db.FeatureFlags.Add(new FeatureFlagRecord
        {
            Name = TaskIoSnapshotRecorder.FeatureFlagName,
            Enabled = flagEnabled
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return db;
    }

    [Fact]
    public async Task FlagDisabled_WritesNoEvent()
    {
        await using var db = await NewDbAsync(flagEnabled: false);
        var recorder = new TaskIoSnapshotRecorder(db, new ConnectorRedactionPolicy());

        await recorder.RecordAsync(
            Guid.NewGuid(), "serviceTask_1", "tenant-a",
            new Dictionary<string, object> { ["id"] = 5 }, output: null,
            success: true, errorMessage: null, TestContext.Current.CancellationToken);

        Assert.Empty(await db.HistoryEvents.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FlagEnabled_RedactsSensitiveKeysAndPersistsEvent()
    {
        await using var db = await NewDbAsync(flagEnabled: true);
        var recorder = new TaskIoSnapshotRecorder(db, new ConnectorRedactionPolicy());
        var processInstanceId = Guid.NewGuid();

        await recorder.RecordAsync(
            processInstanceId, "serviceTask_1", "tenant-a",
            new Dictionary<string, object> { ["user"] = "yova", ["apiToken"] = "super-secret-value" },
            new Dictionary<string, object> { ["status"] = "ok" },
            success: true, errorMessage: null, TestContext.Current.CancellationToken);

        var evt = Assert.Single(await db.HistoryEvents.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(TaskIoSnapshotRecorder.EventType, evt.EventType);
        Assert.Equal("serviceTask_1", evt.ElementId);
        Assert.Equal(processInstanceId, evt.ProcessInstanceId);
        Assert.Equal("tenant-a", evt.TenantId);
        Assert.DoesNotContain("super-secret-value", evt.Data, StringComparison.Ordinal);
        Assert.Contains("\"***\"", evt.Data, StringComparison.Ordinal);
        Assert.Contains("yova", evt.Data, StringComparison.Ordinal);
    }
}
