using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Fixture ownership is transferred to callers and disposed with await using.")]
public sealed class ValidatedTaskCompletionRuntimeTests
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "This persistent fixture is also executed directly by the isolated console TDD harness, outside TestContext.")]
    [Fact]
    public async Task ReusedKeyWithChangedPayload_ConflictsWithoutAnotherEffect()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var runtime = CompletionFixture.Runtime(db);
        await runtime.CompleteUserTaskAsync(fixture.TaskId, Values("first"), "same-key");
        var error = await Record.ExceptionAsync(() => runtime.CompleteUserTaskAsync(fixture.TaskId, Values("changed"), "same-key").AsTask());
        Assert.NotNull(error);
        Assert.Equal("TaskCompletionConflictException", error.GetType().Name);
        await fixture.AssertEffectsAsync(1);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "This persistent fixture is also executed directly by the isolated console TDD harness, outside TestContext.")]
    [Fact]
    public async Task Completion_RechecksAssigneeAtRuntimeBoundary()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var runtime = CompletionFixture.Runtime(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => runtime.CompleteValidatedAsync(
            fixture.TaskId, Values("first"), "key", null, null, "mallory", "tenant-a", false).AsTask());
        await fixture.AssertEffectsAsync(0);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "This persistent fixture is also executed directly by the isolated console TDD harness, outside TestContext.")]
    [Fact]
    public async Task LegacyCompletion_CannotBypassBoundTaskFence()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var task = await db.Tasks.SingleAsync(t => t.Id == fixture.TaskId);
        task.FormSchema = """{"components":[{"type":"textfield","key":"reason"}]}""";
        task.FormSchemaHash = "frozen";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => CompletionFixture.Runtime(db).CompleteUserTaskAsync(
            fixture.TaskId, Values("first"), "key").AsTask());
        await fixture.AssertEffectsAsync(0);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "This persistent fixture is also executed directly by the isolated console TDD harness, outside TestContext.")]
    [Fact]
    public async Task CandidateRole_AllowsMatchingCaller()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var task = await db.Tasks.SingleAsync(t => t.Id == fixture.TaskId);
        task.Assignee = null;
        task.CandidateRole = "Approver";
        await db.SaveChangesAsync();
        await CompletionFixture.Runtime(db).CompleteValidatedAsync(fixture.TaskId, Values("first"), "key",
            null, null, "alice", "tenant-a", false, ["Approver"]);
        await fixture.AssertEffectsAsync(1);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "This persistent fixture is also executed directly by the isolated console TDD harness, outside TestContext.")]
    [Fact]
    public async Task BoundReplay_RejectsChangedSnapshotMetadata()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var task = await fixture.BindAsync(db);
        var runtime = CompletionFixture.Runtime(db);
        await runtime.CompleteValidatedAsync(task.Id, Values("first"), "key", task.Revision, "frozen", "alice", "tenant-a", false);
        await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.TaskCompletionConflictException>(() => runtime.CompleteValidatedAsync(
            task.Id, Values("first"), "key", 999, "other", "alice", "tenant-a", false).AsTask());
        await fixture.AssertEffectsAsync(1);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "This persistent fixture is also executed directly by the isolated console TDD harness, outside TestContext.")]
    [Fact]
    public async Task ApplicationValidatedCompletion_ReplayDoesNotEmitSecondMiningEvent()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var sink = new Mock<IProcessMiningEventSink>();
        var service = new VertexBPMN.Application.TaskService(new VertexBPMN.Infrastructure.Persistence.Repositories.TaskRepository(db),
            sink.Object, CompletionFixture.Runtime(db));
        var completion = Assert.IsAssignableFrom<ITaskCompletionService>(service);
        await completion.CompleteValidatedAsync(fixture.TaskId, Values("first"), "key", null, null, "alice", "tenant-a", false);
        await completion.CompleteValidatedAsync(fixture.TaskId, Values("first"), "key", null, null, "alice", "tenant-a", false);
        sink.Verify(s => s.EmitAsync(It.IsAny<ProcessMiningEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        await fixture.AssertEffectsAsync(1);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "This persistent fixture is also executed directly by the isolated console TDD harness, outside TestContext.")]
    [Fact]
    public async Task Claim_AdvancesRevisionInvalidatingOfflineCompletion()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var before = await db.Tasks.AsNoTracking().SingleAsync(t => t.Id == fixture.TaskId);
        var service = new VertexBPMN.Application.TaskService(new VertexBPMN.Infrastructure.Persistence.Repositories.TaskRepository(db),
            Mock.Of<IProcessMiningEventSink>(), CompletionFixture.Runtime(db));
        await service.ClaimAsync(fixture.TaskId, "bob");
        db.ChangeTracker.Clear();
        Assert.Equal(before.Revision + 1, (await db.Tasks.SingleAsync(t => t.Id == fixture.TaskId)).Revision);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "Also executed outside TestContext in the isolated harness.")]
    [Fact]
    public async Task Delegate_AdvancesRevisionInvalidatingOfflineCompletion()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var before = await db.Tasks.AsNoTracking().SingleAsync(t => t.Id == fixture.TaskId);
        var service = new VertexBPMN.Application.TaskService(new VertexBPMN.Infrastructure.Persistence.Repositories.TaskRepository(db),
            Mock.Of<IProcessMiningEventSink>(), CompletionFixture.Runtime(db));
        await service.DelegateAsync(fixture.TaskId, "bob");
        db.ChangeTracker.Clear();
        Assert.Equal(before.Revision + 1, (await db.Tasks.SingleAsync(t => t.Id == fixture.TaskId)).Revision);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "Also executed outside TestContext in the isolated harness.")]
    [Theory]
    [InlineData("revision")]
    [InlineData("hash")]
    [InlineData("key")]
    [InlineData("tenant")]
    [InlineData("identity")]
    [InlineData("candidate")]
    [InlineData("role")]
    public async Task RejectedBoundary_LeavesAllPersistentEffectsUnchanged(string rejection)
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var task = await fixture.BindAsync(db);
        if (rejection is "candidate" or "role")
        {
            task.Assignee = null;
            if (rejection == "candidate")
            {
                task.CandidateUsers = ["bob"];
            }
            else
            {
                task.CandidateRole = "Approver";
            }
            await db.SaveChangesAsync();
        }
        var error = await Record.ExceptionAsync(() => CompletionFixture.Runtime(db).CompleteValidatedAsync(task.Id, Values("first"),
            rejection == "key" ? null : "key", rejection == "revision" ? task.Revision - 1 : task.Revision,
            rejection == "hash" ? "other" : "frozen", rejection == "identity" ? null : "alice",
            rejection == "tenant" ? "tenant-b" : "tenant-a", false).AsTask());
        Assert.NotNull(error);
        if (rejection is "revision" or "hash")
        {
            Assert.IsType<VertexBPMN.Domain.Exceptions.TaskCompletionConflictException>(error);
        }
        else if (rejection == "key")
        {
            Assert.IsType<ArgumentException>(error);
        }
        else
        {
            Assert.IsType<UnauthorizedAccessException>(error);
        }
        await fixture.AssertEffectsAsync(0);
        await using var check = fixture.Open();
        Assert.Equal(UserTaskStatus.Pending, (await check.Tasks.SingleAsync(t => t.Id == task.Id)).Status);
        Assert.False(await check.Variables.AnyAsync(v => v.ProcessInstanceId == fixture.InstanceId && v.Name == "reason"));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "Also executed outside TestContext in the isolated harness.")]
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentSqliteCompletion_ProducesExactlyOnePersistentEffect(bool identical)
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        long revision;
        await using (var db = fixture.Open())
        {
            revision = (await fixture.BindAsync(db)).Revision;
        }
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () =>
        {
            await using var db = fixture.Open();
            await ready.Task;
            return await Record.ExceptionAsync(() => CompletionFixture.Runtime(db).CompleteValidatedAsync(fixture.TaskId,
                Values("first"), "key", revision, "frozen", "alice", "tenant-a", false).AsTask());
        });
        var second = Task.Run(async () =>
        {
            await using var db = fixture.Open();
            await ready.Task;
            return await Record.ExceptionAsync(() => CompletionFixture.Runtime(db).CompleteValidatedAsync(fixture.TaskId,
                Values(identical ? "first" : "changed"), identical ? "key" : "other-key", revision,
                "frozen", "alice", "tenant-a", false).AsTask());
        });
        ready.SetResult();
        var errors = await Task.WhenAll(first, second);
        Assert.Equal(identical ? 0 : 1, errors.Count(e => e is not null));
        if (!identical)
        {
            Assert.IsType<VertexBPMN.Domain.Exceptions.TaskCompletionConflictException>(errors.Single(e => e is not null));
        }
        await fixture.AssertEffectsAsync(1);
        await using var check = fixture.Open();
        Assert.Equal(UserTaskStatus.Completed, (await check.Tasks.SingleAsync(t => t.Id == fixture.TaskId)).Status);
        Assert.Equal(ProcessInstanceStatus.Completed, (await check.ProcessInstances.SingleAsync(i => i.Id == fixture.InstanceId)).Status);
        Assert.Equal(revision + 1, (await check.Tasks.SingleAsync(t => t.Id == fixture.TaskId)).Revision);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "Also executed outside TestContext in the isolated harness.")]
    [Fact]
    public async Task SameKey_OnDifferentTasksDoesNotSwallowSecondCompletion()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var original = await db.Tasks.SingleAsync(t => t.Id == fixture.TaskId);
        var definition = await db.ProcessDefinitions.SingleAsync(d => d.Key == "completion");
        var runtime = CompletionFixture.Runtime(db);
        var secondInstance = await runtime.StartAsync(definition, null, null, "tenant-a");
        var second = await db.Tasks.SingleAsync(t => t.ProcessInstanceId == secondInstance.Id);
        await runtime.CompleteUserTaskAsync(original.Id, Values("first"), "shared-key");
        await runtime.CompleteUserTaskAsync(second.Id, Values("second"), "shared-key");
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.Tasks.CountAsync(t => t.Status == UserTaskStatus.Completed && (t.Id == original.Id || t.Id == second.Id)));
        Assert.Equal(2, await db.RuntimeInbox.CountAsync(i => i.Operation.StartsWith("complete-task") && i.CompletedAt != null));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "Also executed outside TestContext in the isolated harness.")]
    [Fact]
    public async Task SequentialBoundReplay_ReloadsAuthorizationAndCanonicalPayload()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var task = await fixture.BindAsync(db);
        var revision = task.Revision;
        var runtime = CompletionFixture.Runtime(db);
        var values = new Dictionary<string, object>(StringComparer.Ordinal) { ["reason"] = "first" };
        await runtime.CompleteValidatedAsync(task.Id, values, "key", revision, "frozen", "alice", "tenant-a", false);
        await using (var reopened = fixture.Open())
        {
            await CompletionFixture.Runtime(reopened).CompleteValidatedAsync(task.Id,
                new Dictionary<string, object>(StringComparer.Ordinal) { ["reason"] = System.Text.Json.JsonSerializer.SerializeToElement("first") },
                "key", revision, "frozen", "alice", "tenant-a", false);
        }
        await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.TaskCompletionConflictException>(async () =>
        {
            await runtime.CompleteValidatedAsync(task.Id, values, "different", revision, "frozen", "alice", "tenant-a", false);
        });
        await using (var other = fixture.Open())
        {
            var reassigned = await other.Tasks.SingleAsync(t => t.Id == task.Id);
            reassigned.Assignee = "bob";
            reassigned.Revision++;
            await other.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => runtime.CompleteValidatedAsync(
            task.Id, values, "key", revision, "frozen", "alice", "tenant-a", false).AsTask());
        await fixture.AssertEffectsAsync(1);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051", Justification = "Also executed outside TestContext in the isolated harness.")]
    [Fact]
    public async Task BoundCompletion_RequiresIdentityEvenForAdmin()
    {
        await using var fixture = await CompletionFixture.CreateAsync();
        await using var db = fixture.Open();
        var task = await fixture.BindAsync(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CompletionFixture.Runtime(db).CompleteValidatedAsync(task.Id,
            Values("first"), "key", task.Revision, "frozen", null, "tenant-a", true).AsTask());
        await fixture.AssertEffectsAsync(0);
    }

    private static Dictionary<string, object> Values(string value) => new(StringComparer.Ordinal) { ["reason"] = value };

    private sealed class CompletionFixture(string path, Guid taskId, Guid instanceId) : IAsyncDisposable
    {
        public Guid TaskId { get; } = taskId;
        public Guid InstanceId { get; } = instanceId;
        public BpmnDbContext Open() => new(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite($"Data Source={path};Default Timeout=10").Options);
        public static PersistentProcessExecutionRuntime Runtime(BpmnDbContext db) => new(db, Mock.Of<IServiceTaskRegistry>(), Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance);

        public static async Task<CompletionFixture> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lc04-completion-{Guid.NewGuid():N}.db");
            var setup = new CompletionFixture(path, Guid.Empty, Guid.Empty);
            await using var db = setup.Open();
            await db.Database.EnsureCreatedAsync();
            var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "completion", CreatedAt = DateTime.UtcNow };
            var definition = new ProcessDefinition
            {
                Id = Guid.NewGuid(), Key = "completion", Version = 1, TenantId = "tenant-a", Deployment = deployment, DeploymentId = deployment.Id,
                BpmnXml = """<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"><process id="completion" isExecutable="true"><startEvent id="start"/><sequenceFlow id="f1" sourceRef="start" targetRef="approval"/><userTask id="approval"/><sequenceFlow id="f2" sourceRef="approval" targetRef="end"/><endEvent id="end"/></process></definitions>"""
            };
            db.ProcessDefinitions.Add(definition);
            await db.SaveChangesAsync();
            var instance = await Runtime(db).StartAsync(definition, null, null, "tenant-a");
            var task = await db.Tasks.SingleAsync(t => t.ProcessInstanceId == instance.Id);
            task.Assignee = "alice";
            await db.SaveChangesAsync();
            return new CompletionFixture(path, task.Id, instance.Id);
        }

        public async Task<UserTask> BindAsync(BpmnDbContext db)
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == TaskId);
            task.FormSchema = """{"components":[{"type":"textfield","key":"reason"}]}""";
            task.FormSchemaHash = "frozen";
            await db.SaveChangesAsync();
            return task;
        }

        public async Task AssertEffectsAsync(int count)
        {
            await using var db = Open();
            Assert.Equal(count, await db.HistoryEvents.CountAsync(h => h.ProcessInstanceId == InstanceId && h.EventType == "USER_TASK_COMPLETED"));
            Assert.Equal(count, await db.RuntimeOutbox.CountAsync(h => h.ProcessInstanceId == InstanceId && h.EventType == "UserTaskCompleted"));
            Assert.Equal(count, await db.RuntimeInbox.CountAsync(h => h.CompletedAt != null && h.Operation.StartsWith("complete-task")));
        }

        public ValueTask DisposeAsync()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
            return ValueTask.CompletedTask;
        }
    }
}
