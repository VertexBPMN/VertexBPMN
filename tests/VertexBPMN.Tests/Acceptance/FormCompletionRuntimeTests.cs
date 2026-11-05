using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

public sealed class FormCompletionRuntimeTests
{
    [Fact]
    public async Task InvalidCompletion_LeavesPersistentTaskAndVariablesUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(ct);
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "forms", CreatedAt = DateTime.UtcNow };
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = "form-completion", Version = 1, TenantId = "tenant-a",
            Deployment = deployment, DeploymentId = deployment.Id, CreatedAt = DateTime.UtcNow,
            BpmnXml = """
                <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
                  <process id="form-completion" isExecutable="true">
                    <startEvent id="start"/><sequenceFlow id="f1" sourceRef="start" targetRef="approval"/>
                    <userTask id="approval"/><sequenceFlow id="f2" sourceRef="approval" targetRef="end"/><endEvent id="end"/>
                  </process>
                </definitions>
                """
        };
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        var runtime = new PersistentProcessExecutionRuntime(db, Mock.Of<IServiceTaskRegistry>(),
            Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance);
        var instance = await runtime.StartAsync(definition, null, null, "tenant-a", cancellationToken: ct);
        var task = await db.Tasks.SingleAsync(t => t.ProcessInstanceId == instance.Id, ct);
        task.FormKey = "approval";
        task.FormSchema = """{"components":[{"type":"textfield","key":"reason","validate":{"required":true}}]}""";
        await db.SaveChangesAsync(ct);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => runtime.CompleteUserTaskAsync(task.Id,
            new Dictionary<string, object>(StringComparer.Ordinal), "invalid-attempt", ct).AsTask());

        db.ChangeTracker.Clear();
        Assert.Equal(UserTaskStatus.Pending, (await db.Tasks.SingleAsync(t => t.Id == task.Id, ct)).Status);
        Assert.Equal(ProcessInstanceStatus.Running, (await db.ProcessInstances.SingleAsync(i => i.Id == instance.Id, ct)).Status);
        Assert.False(await db.RuntimeInbox.AnyAsync(i => i.IdempotencyKey == "invalid-attempt", ct));
        Assert.False(await db.Variables.AnyAsync(v => v.ProcessInstanceId == instance.Id && v.Name == "reason", ct));
    }
}
