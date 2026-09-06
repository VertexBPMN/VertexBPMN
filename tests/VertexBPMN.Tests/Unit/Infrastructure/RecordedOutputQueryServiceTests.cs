using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class RecordedOutputQueryServiceTests
{
    private static BpmnDbContext NewDb()
        => new(new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task GetLastRecordedOutput_ReturnsLatestOutput_AcrossInstancesOfSameDefinition()
    {
        await using var db = NewDb();
        var def = new ProcessDefinition { Key = "orderProc", TenantId = "t1", TenantScope = "t1", Version = 1 };
        await db.ProcessDefinitions.AddAsync(def);

        var older = new ProcessInstance { ProcessDefinitionId = def.Id, TenantId = "t1", ProcessId = "orderProc" };
        var newer = new ProcessInstance { ProcessDefinitionId = def.Id, TenantId = "t1", ProcessId = "orderProc" };
        await db.ProcessInstances.AddRangeAsync(older, newer);

        await db.HistoryEvents.AddRangeAsync(
            new HistoryEvent
            {
                ProcessInstanceId = older.Id,
                EventType = TaskIoSnapshotRecorder.EventType,
                ElementId = "callApi",
                TenantId = "t1",
                Timestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                Data = """{"output":{"a":1}}"""
            },
            new HistoryEvent
            {
                ProcessInstanceId = newer.Id,
                EventType = TaskIoSnapshotRecorder.EventType,
                ElementId = "callApi",
                TenantId = "t1",
                Timestamp = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
                Data = """{"output":{"a":2,"b":"x"}}"""
            });
        await db.SaveChangesAsync();

        var service = new RecordedOutputQueryService(db);
        var output = await service.GetLastRecordedOutputAsync("t1", "orderProc", "callApi");

        Assert.NotNull(output);
        Assert.Equal(2m, output!["a"]);
        Assert.Equal("x", output["b"]);
    }

    [Fact]
    public async Task GetLastRecordedOutput_UnknownElementOrTenant_ReturnsNull()
    {
        await using var db = NewDb();
        var def = new ProcessDefinition { Key = "orderProc", TenantId = "t1", TenantScope = "t1", Version = 1 };
        await db.ProcessDefinitions.AddAsync(def);
        var instance = new ProcessInstance { ProcessDefinitionId = def.Id, TenantId = "t1", ProcessId = "orderProc" };
        await db.ProcessInstances.AddAsync(instance);
        await db.HistoryEvents.AddAsync(new HistoryEvent
        {
            ProcessInstanceId = instance.Id,
            EventType = TaskIoSnapshotRecorder.EventType,
            ElementId = "callApi",
            TenantId = "t1",
            Timestamp = DateTime.UtcNow,
            Data = """{"output":{"a":1}}"""
        });
        await db.SaveChangesAsync();

        var service = new RecordedOutputQueryService(db);
        var otherDef = await service.GetLastRecordedOutputAsync("t1", "orderProc", "otherTask");
        var otherTenant = await service.GetLastRecordedOutputAsync("t2", "orderProc", "callApi");

        Assert.Null(otherDef);
        Assert.Null(otherTenant);
    }
}
