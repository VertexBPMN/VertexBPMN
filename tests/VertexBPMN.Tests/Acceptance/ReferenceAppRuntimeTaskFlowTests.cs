using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Application;
using VertexBPMN.Application.Lc06;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// End-to-end task lifecycle for the LC06 reference application's real BPMN process:
/// starting the process through the persistent engine creates a personal inbox task
/// (candidate role = FinanceAgent), which the inbox surfaces, can be claimed, and then
/// completed. Exercises the real BpmnDbContext, PersistentProcessExecutionRuntime,
/// TaskRepository and TaskService - no mocks on the engine/task path.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Fixture ownership is transferred to callers and disposed with await using.")]
public sealed class ReferenceAppRuntimeTaskFlowTests
{
    private static readonly string[] FinanceAgentRole = { Lc06ReferenceApp.CandidateRole };
    private static readonly string[] OtherRole = { "OtherRole" };

    [Fact]
    public async Task Start_CreatesPersonalInboxTask_ThenClaim_And_Complete()
    {
        await using var fixture = await TaskFlowFixture.CreateAsync();
        var ct = TestContext.Current.CancellationToken;

        Guid instanceId;
        Guid taskId;
        await using (var db = fixture.Open())
        {
            var runtime = TaskFlowFixture.Runtime(db);
            var instance = await runtime.StartAsync(fixture.Definition, null, null, Lc06ReferenceApp.TenantId, cancellationToken: ct);
            instanceId = instance.Id;

            var task = await db.Tasks.SingleAsync(t => t.ProcessInstanceId == instance.Id, ct);
            taskId = task.Id;
            Assert.Equal(UserTaskStatus.Pending, task.Status);
            Assert.Equal(Lc06ReferenceApp.CandidateRole, task.CandidateRole);
            Assert.Null(task.Assignee);
        }

        // The task must surface in the personal inbox for a FinanceAgent, and be hidden
        // from a non-matching identity.
        await using (var db = fixture.Open())
        {
            var repository = new TaskRepository(db);
            var visible = await repository.QueryInboxAsync(
                new TaskInboxQuery(Lc06ReferenceApp.TenantId, "finance-user", FinanceAgentRole), ct);
            Assert.Contains(visible.Items, item => item.Id == taskId);

            var hidden = await repository.QueryInboxAsync(
                new TaskInboxQuery(Lc06ReferenceApp.TenantId, "someone-else", OtherRole), ct);
            Assert.DoesNotContain(hidden.Items, item => item.Id == taskId);
        }

        // Claim then complete through the real service+runtime.
        await using (var db = fixture.Open())
        {
            var runtime = TaskFlowFixture.Runtime(db);
            var service = new TaskService(new TaskRepository(db), Mock.Of<IProcessMiningEventSink>(), runtime);

            await service.ClaimAsync(taskId, "finance-user", ct);
            await service.CompleteValidatedAsync(
                taskId, new Dictionary<string, object>(StringComparer.Ordinal), "lc06-complete", null, null,
                "finance-user", Lc06ReferenceApp.TenantId, false, FinanceAgentRole, ct);

            var completed = await db.Tasks.SingleAsync(t => t.Id == taskId, ct);
            Assert.Equal(UserTaskStatus.Completed, completed.Status);
            Assert.Equal("finance-user", completed.Assignee);

            var history = await db.HistoryEvents.SingleAsync(
                h => h.ProcessInstanceId == instanceId && h.EventType == "USER_TASK_COMPLETED", ct);
            Assert.NotNull(history);
        }
    }

    private sealed class TaskFlowFixture(string path, ProcessDefinition definition) : IAsyncDisposable
    {
        public ProcessDefinition Definition { get; } = definition;

        public BpmnDbContext Open()
            => new(new DbContextOptionsBuilder<BpmnDbContext>()
                .UseSqlite($"Data Source={path};Default Timeout=10").Options);

        public static PersistentProcessExecutionRuntime Runtime(BpmnDbContext db)
            => new(db, Mock.Of<IServiceTaskRegistry>(), Mock.Of<IDecisionService>(),
                NullLogger<PersistentProcessExecutionRuntime>.Instance);

        public static async Task<TaskFlowFixture> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lc06-taskflow-{Guid.NewGuid():N}.db");
            var setup = new TaskFlowFixture(path, null!);
            var ct = TestContext.Current.CancellationToken;
            await using var db = setup.Open();
            await db.Database.EnsureCreatedAsync(ct);

            var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "lc06-reference-deployment", CreatedAt = DateTime.UtcNow };
            var referenceProcess = new ProcessDefinition
            {
                Id = Guid.Parse(Lc06ReferenceApp.ProcessId),
                Key = "lc06-reference-flow",
                Version = 1,
                TenantId = Lc06ReferenceApp.TenantId,
                Deployment = deployment,
                DeploymentId = deployment.Id,
                BpmnXml = Lc06ReferenceApp.ProcessBpmn
            };
            db.ProcessDefinitions.Add(referenceProcess);
            await db.SaveChangesAsync(ct);
            return new TaskFlowFixture(path, referenceProcess);
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // best-effort cleanup
            }

            return ValueTask.CompletedTask;
        }
    }
}
