using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Application.Offline;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;

namespace VertexBPMN.Tests.Unit.Offline;

/// <summary>
/// LC11 (PWA / offline, Stufe D) - Slice 2: der serverseitige Replay-Vertrag für die
/// Offline-Completion-Queue. Nach Reconnect wird jede erledigte Aufgabe höchstens EINMAL
/// serverseitig abgeschlossen (LC-AC-19); veraltete/entzogene/schema-geänderte Items werden als
/// sichtbarer Konflikt zurückgegeben - nie automatisch überschrieben oder doppelt ausgeführt.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "Persistent SQLite fixture executes completion/replay outside TestContext, matching ValidatedTaskCompletionRuntimeTests.")]
public sealed class OfflineCompletionReplayServiceTests
{
    [Fact]
    public async Task Replay_AppliesEachValidItemExactlyOnce_AndReplaysIdempotently()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        var items = new[]
        {
            new OfflineCompletionCommand(fixture.TaskA, Values("first"), "key-a", fixture.RevisionA, "frozen"),
            new OfflineCompletionCommand(fixture.TaskB, Values("first"), "key-b", fixture.RevisionB, "frozen"),
        };

        var first = await fixture.Replay.ReplayAsync(items, "alice", "tenant-a", false, null);
        Assert.All(first, r => Assert.Equal(OfflineCompletionStatus.Completed, r.Status));
        await fixture.AssertEffectsAsync(fixture.InstanceA, 1);
        await fixture.AssertEffectsAsync(fixture.InstanceB, 1);

        // A reconnected client may resend the whole queue; it must be idempotent (no second effect).
        var second = await fixture.Replay.ReplayAsync(items, "alice", "tenant-a", false, null);
        Assert.All(second, r => Assert.Equal(OfflineCompletionStatus.Completed, r.Status));
        await fixture.AssertEffectsAsync(fixture.InstanceA, 1);
        await fixture.AssertEffectsAsync(fixture.InstanceB, 1);
    }

    [Fact]
    public async Task Replay_StaleRevision_ReturnsConflict_WithoutEffect()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        var result = await fixture.Replay.ReplayAsync(
            [new OfflineCompletionCommand(fixture.TaskA, Values("first"), "key-a", fixture.RevisionA - 1, "frozen")],
            "alice", "tenant-a", false, null);
        var item = Assert.Single(result);
        Assert.Equal(OfflineCompletionStatus.Conflict, item.Status);
        Assert.Equal(fixture.RevisionA, item.Revision);
        await fixture.AssertEffectsAsync(fixture.InstanceA, 0);
    }

    [Fact]
    public async Task Replay_ChangedSchemaHash_ReturnsConflict_WithoutEffect()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        var result = await fixture.Replay.ReplayAsync(
            [new OfflineCompletionCommand(fixture.TaskA, Values("first"), "key-a", fixture.RevisionA, "other-hash")],
            "alice", "tenant-a", false, null);
        var item = Assert.Single(result);
        Assert.Equal(OfflineCompletionStatus.Conflict, item.Status);
        await fixture.AssertEffectsAsync(fixture.InstanceA, 0);
    }

    [Fact]
    public async Task Replay_UnknownTask_ReturnsNotFound()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        var result = await fixture.Replay.ReplayAsync(
            [new OfflineCompletionCommand(Guid.NewGuid(), Values("first"), "key-a", null, null)],
            "alice", "tenant-a", false, null);
        Assert.Equal(OfflineCompletionStatus.NotFound, Assert.Single(result).Status);
    }

    [Fact]
    public async Task Replay_ForeignTenant_ReturnsForbidden_WithoutEffect()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        var result = await fixture.Replay.ReplayAsync(
            [new OfflineCompletionCommand(fixture.TaskA, Values("first"), "key-a", fixture.RevisionA, "frozen")],
            "bob", "tenant-b", false, null);
        var item = Assert.Single(result);
        Assert.Equal(OfflineCompletionStatus.Forbidden, item.Status);
        await fixture.AssertEffectsAsync(fixture.InstanceA, 0);
    }

    [Fact]
    public async Task Replay_SameKeyWithChangedPayload_Conflicts_KeepingSingleEffect()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        await fixture.Replay.ReplayAsync(
            [new OfflineCompletionCommand(fixture.TaskA, Values("first"), "key-a", fixture.RevisionA, "frozen")],
            "alice", "tenant-a", false, null);
        await fixture.AssertEffectsAsync(fixture.InstanceA, 1);

        var result = await fixture.Replay.ReplayAsync(
            [new OfflineCompletionCommand(fixture.TaskA, Values("changed"), "key-a", fixture.RevisionA, "frozen")],
            "alice", "tenant-a", false, null);
        Assert.Equal(OfflineCompletionStatus.Conflict, Assert.Single(result).Status);
        await fixture.AssertEffectsAsync(fixture.InstanceA, 1);
    }

    private static Dictionary<string, object> Values(string value) => new(StringComparer.Ordinal) { ["reason"] = value };

    private sealed class ReplayFixture : IAsyncDisposable
    {
        public Guid TaskA { get; }
        public Guid TaskB { get; }
        public Guid InstanceA { get; }
        public Guid InstanceB { get; }
        public long RevisionA { get; }
        public long RevisionB { get; }
        public IOfflineCompletionReplayService Replay { get; }
        private readonly string _path;
        private readonly BpmnDbContext _live;

        private ReplayFixture(string path, BpmnDbContext live, Guid taskA, Guid taskB, Guid instanceA, Guid instanceB,
            long revisionA, long revisionB, IOfflineCompletionReplayService replay)
        {
            _path = path;
            _live = live;
            TaskA = taskA; TaskB = taskB; InstanceA = instanceA; InstanceB = instanceB;
            RevisionA = revisionA; RevisionB = revisionB; Replay = replay;
        }

        public BpmnDbContext Open() => new(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite($"Data Source={_path};Default Timeout=10").Options);

        public static PersistentProcessExecutionRuntime Runtime(BpmnDbContext db) => new(db,
            Mock.Of<IServiceTaskRegistry>(), Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance);

        public static async Task<ReplayFixture> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lc11-replay-{Guid.NewGuid():N}.db");
            var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite($"Data Source={path};Default Timeout=10").Options);
            await db.Database.EnsureCreatedAsync();
            var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "completion", CreatedAt = DateTime.UtcNow };
            var definition = new ProcessDefinition
            {
                Id = Guid.NewGuid(), Key = "completion", Version = 1, TenantId = "tenant-a", Deployment = deployment, DeploymentId = deployment.Id,
                BpmnXml = """<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"><process id="completion" isExecutable="true"><startEvent id="start"/><sequenceFlow id="f1" sourceRef="start" targetRef="approval"/><userTask id="approval"/><sequenceFlow id="f2" sourceRef="approval" targetRef="end"/><endEvent id="end"/></process></definitions>"""
            };
            db.ProcessDefinitions.Add(definition);
            await db.SaveChangesAsync();
            var runtime = Runtime(db);
            var instanceA = await runtime.StartAsync(definition, null, null, "tenant-a");
            var instanceB = await runtime.StartAsync(definition, null, null, "tenant-a");
            var taskA = await db.Tasks.SingleAsync(t => t.ProcessInstanceId == instanceA.Id);
            var taskB = await db.Tasks.SingleAsync(t => t.ProcessInstanceId == instanceB.Id);
            taskA.Assignee = "alice";
            taskA.FormSchema = """{"components":[{"type":"textfield","key":"reason"}]}""";
            taskA.FormSchemaHash = "frozen";
            taskB.Assignee = "alice";
            taskB.FormSchema = """{"components":[{"type":"textfield","key":"reason"}]}""";
            taskB.FormSchemaHash = "frozen";
            await db.SaveChangesAsync();
            var revA = taskA.Revision;
            var revB = taskB.Revision;
            db.ChangeTracker.Clear();

            var taskService = new TaskService(new TaskRepository(db), Mock.Of<IProcessMiningEventSink>(), runtime);
            var replay = new OfflineCompletionReplayService(taskService, taskService);
            return new ReplayFixture(path, db, taskA.Id, taskB.Id, instanceA.Id, instanceB.Id, revA, revB, replay);
        }

        public async Task AssertEffectsAsync(Guid instanceId, int count)
        {
            await using var db = Open();
            Assert.Equal(count, await db.HistoryEvents.CountAsync(h => h.ProcessInstanceId == instanceId && h.EventType == "USER_TASK_COMPLETED"));
        }

        public ValueTask DisposeAsync()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            _live.Dispose();
            File.Delete(_path);
            return ValueTask.CompletedTask;
        }
    }
}
