using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

public sealed class ExternalTaskSchedulingPreviewTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(true, 3)]
    [InlineData(true, 1, true)]
    [InlineData(true, 3, true)]
    [InlineData(true, 1, true, "leased")]
    [InlineData(true, 3, true, "leased")]
    [InlineData(true, 1, true, "accepted")]
    [InlineData(true, 1, false, "ready", true)]
    public async Task RuntimeSchedulesDurableWaitWithoutInvokingHandler(bool enabled, int count, bool terminate = false, string priorState = "ready", bool staleInboxRead = false)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        var staleRead = new StaleInboxReadInterceptor();
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).AddInterceptors(staleRead).Options;
        await using var db = new BpmnDbContext(options);
        await db.Database.EnsureCreatedAsync(ct);
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "external", TenantId = "a" };
        var loop = count > 1 ? $"<multiInstanceLoopCharacteristics isSequential='false'><loopCardinality>{count}</loopCardinality></multiInstanceLoopCharacteristics>" : "";
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = "p", Name = "external", TenantId = "a", TenantScope = "a", Version = 1, DeploymentId = deployment.Id,
            BpmnXml = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
              <process id="p"><startEvent id="s"/><serviceTask id="work"><extensionElements>
                <vertex:externalTask topic="test.work" maxRetries="0" deadlineSeconds="300"/>
                <vertex:ioMapping><vertex:input name="text" expression="document"/></vertex:ioMapping>
              </extensionElements>{{loop}}</serviceTask><endEvent id="e"/>
              <sequenceFlow id="f1" sourceRef="s" targetRef="work"/><sequenceFlow id="f2" sourceRef="work" targetRef="e"/>
              </process></definitions>
            """
        };
        if (terminate)
        {
            definition.BpmnXml = definition.BpmnXml.Replace("<startEvent id=\"s\"/>",
                "<startEvent id=\"s\"/><parallelGateway id=\"fork\"/><userTask id=\"cancel\"/><endEvent id=\"stop\"><terminateEventDefinition/></endEvent>", StringComparison.Ordinal)
                .Replace("sourceRef=\"s\" targetRef=\"work\"", "sourceRef=\"fork\" targetRef=\"work\"", StringComparison.Ordinal)
                .Replace("</process>", "<sequenceFlow id=\"forkIn\" sourceRef=\"s\" targetRef=\"fork\"/><sequenceFlow id=\"cancelIn\" sourceRef=\"fork\" targetRef=\"cancel\"/><sequenceFlow id=\"cancelOut\" sourceRef=\"cancel\" targetRef=\"stop\"/></process>", StringComparison.Ordinal);
        }
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        var registry = new Mock<IServiceTaskRegistry>(MockBehavior.Strict);
        var config = VertexBPMN.Tests.Unit.Infrastructure.ExternalTaskContractResolverTests.Configuration();
        config["ExternalTasks:EnableSchedulingPreview"] = enabled.ToString();
        var runtime = new PersistentProcessExecutionRuntime(db, registry.Object, Mock.Of<IDecisionService>(),
            NullLogger<PersistentProcessExecutionRuntime>.Instance, config, new ConfiguredExternalTaskContractResolver(config));
        var input = new Dictionary<string, object> { ["document"] = "synthetic", ["unmapped"] = "private" };
        if (!enabled)
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await runtime.StartAsync(definition, input, null, "a", "start", ct));
        else
        {
            var started = await runtime.StartAsync(definition, input, null, "a", "start", ct);
            staleRead.Armed = staleInboxRead;
            var duplicate = await runtime.StartAsync(definition, input, null, "a", "start", ct);
            Assert.Equal(started.Id, duplicate.Id);
            Assert.Equal(staleInboxRead ? 1 : 0, staleRead.SuppressedReads);
            if (terminate)
            {
                // Arrange persisted protocol states. Claim/Complete APIs are not implemented yet;
                // this exercises real runtime cancellation, not their future protocol semantics.
                if (priorState != "ready")
                {
                    await using var arrange = new BpmnDbContext(options);
                    foreach (var job in await arrange.ExternalTaskJobs.ToListAsync(ct))
                    {
                        var leaseId = Guid.NewGuid();
                        job.State = priorState == "accepted" ? ExternalTaskState.Completed : ExternalTaskState.Leased;
                        job.AttemptsStarted = 1;
                        job.LeaseGeneration = 1;
                        job.Revision++;
                        arrange.ExternalTaskAttempts.Add(new ExternalTaskAttempt
                        {
                            Id = Guid.NewGuid(), JobId = job.Id, AttemptNumber = 1, LeaseGeneration = 1,
                            LeaseId = leaseId, WorkerIssuer = "test-issuer", WorkerSubject = "worker",
                            StartedAt = job.CreatedAt, EndedAt = priorState == "accepted" ? job.CreatedAt + 1 : null,
                            EndReason = priorState == "accepted" ? "Completed" : null
                        });
                        if (priorState == "accepted")
                        {
                            job.CompletionId = Guid.NewGuid();
                            job.Result = "{\"answer\":42}";
                            job.ResultHash = new string('a', 64);
                            job.CompletedAt = job.CreatedAt + 1;
                            arrange.ExternalTaskContinuations.Add(new ExternalTaskContinuation
                            {
                                Id = Guid.NewGuid(), JobId = job.Id, ActivityExecutionId = job.ActivityExecutionId,
                                Outcome = ExternalTaskOutcome.Success, State = ExternalTaskContinuationState.Pending,
                                CreatedAt = job.CreatedAt + 1, Revision = 1
                            });
                        }
                        else
                        {
                            job.LeaseId = leaseId;
                            job.LeaseExpiresAt = job.CreatedAt + 60000;
                            job.WorkerIssuer = "test-issuer";
                            job.WorkerSubject = "worker";
                        }
                    }
                    await arrange.SaveChangesAsync(ct);
                    db.ChangeTracker.Clear();
                }
                var cancelTask = await db.Tasks.SingleAsync(task => task.ActivityId == "cancel", ct);
                await runtime.CompleteUserTaskAsync(cancelTask.Id, null, "cancel", ct);
                await runtime.CompleteUserTaskAsync(cancelTask.Id, null, "cancel", ct);
            }
        }
        await using var read = new BpmnDbContext(options);
        var jobs = await read.ExternalTaskJobs.ToListAsync(ct);
        Assert.Equal(enabled ? count : 0, jobs.Count);
        Assert.Equal(enabled ? 1 : 0, await read.ProcessInstances.CountAsync(ct));
        if (terminate)
        {
            Assert.Equal(ProcessInstanceStatus.Completed, (await read.ProcessInstances.SingleAsync(ct)).Status);
            Assert.False(await read.MultiInstanceExecutions.AnyAsync(item => item.State == "Active", ct));
            Assert.Equal(priorState == "accepted" ? 0 : count, await read.HistoryEvents.CountAsync(item => item.EventType == "EXTERNAL_TASK_CANCELLED", ct));
            Assert.False(await read.ExternalTaskAttempts.AnyAsync(item => item.EndedAt == null, ct));
            if (priorState == "accepted")
            {
                var continuation = await read.ExternalTaskContinuations.SingleAsync(ct);
                Assert.Equal(ExternalTaskContinuationState.Cancelled, continuation.State);
                Assert.Equal(2, continuation.Revision);
                Assert.False((await read.ProcessInstances.SingleAsync(ct)).Variables.ContainsKey("answer"));
            }
        }
        foreach (var job in jobs)
        {
            var wait = await read.ExecutionTokens.SingleAsync(token => token.Id == job.WaitTokenId, ct);
            Assert.Equal(terminate ? ExecutionToken.CompletedState : ExecutionToken.WaitingState, wait.State);
            Assert.Equal(priorState == "accepted" ? ExternalTaskState.Completed : terminate ? ExternalTaskState.Cancelled : ExternalTaskState.Ready, job.State);
            if (priorState == "leased")
            {
                Assert.Null(job.LeaseId);
                Assert.Null(job.LeaseExpiresAt);
                Assert.Null(job.WorkerSubject);
                Assert.Equal("Cancelled", (await read.ExternalTaskAttempts.SingleAsync(item => item.JobId == job.Id, ct)).EndReason);
            }
            if (priorState == "accepted")
            {
                Assert.NotNull(job.CompletionId);
                Assert.Equal("{\"answer\":42}", job.Result);
                Assert.Equal(new string('a', 64), job.ResultHash);
            }
            Assert.Equal(job.ActivityExecutionId, wait.ActivityExecutionId);
            Assert.Equal("{\"text\":\"synthetic\"}", job.InputSnapshot);
            Assert.Equal(300000, job.Deadline - job.CreatedAt);
            Assert.Equal(1, job.MaxAttempts);
            using var snapshot = System.Text.Json.JsonDocument.Parse(job.DefinitionSnapshot);
            static string Hash(string value) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
            Assert.Equal(Hash(job.InputSnapshot), snapshot.RootElement.GetProperty("inputSha256").GetString());
            Assert.Equal(Hash(job.SchemaSnapshot), snapshot.RootElement.GetProperty("schemaSha256").GetString());
            var definitionContent = System.Text.Json.JsonSerializer.Serialize(new
            {
                definition = snapshot.RootElement.GetProperty("definition"), mappings = snapshot.RootElement.GetProperty("mappings")
            });
            Assert.Equal(Hash(definitionContent), snapshot.RootElement.GetProperty("definitionSha256").GetString());
        }
        Assert.Equal(jobs.Count, jobs.Select(job => job.ActivityExecutionId).Distinct().Count());
        registry.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(787)]
    [InlineData(2067)]
    public async Task UnrelatedClaimFailureIsNotReclassifiedAsIdempotencyConflict(int extendedCode)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        var failure = new DbUpdateException("deliberate unrelated write failure",
            new SqliteException("UNIQUE constraint failed: OtherTable.Id", 19, extendedCode));
        var interceptor = new ClaimFailureInterceptor(failure);
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options;
        await using var db = new BpmnDbContext(options);
        await db.Database.EnsureCreatedAsync(ct);
        var registry = new Mock<IServiceTaskRegistry>(MockBehavior.Strict);
        var runtime = new PersistentProcessExecutionRuntime(db, registry.Object, Mock.Of<IDecisionService>(),
            NullLogger<PersistentProcessExecutionRuntime>.Instance);
        var definition = new ProcessDefinition { Id = Guid.NewGuid(), TenantId = "a" };
        var error = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await runtime.StartAsync(definition, null, null, "a", "failure", ct));
        Assert.Same(failure, error);
        Assert.Equal(1, interceptor.Calls);
        await using var read = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        Assert.Empty(await read.RuntimeInbox.ToListAsync(ct));
        Assert.Empty(await read.ProcessInstances.ToListAsync(ct));
        registry.VerifyNoOtherCalls();
    }

    private sealed class ClaimFailureInterceptor(DbUpdateException failure) : SaveChangesInterceptor
    {
        public int Calls { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw failure;
        }
    }

    private sealed class StaleInboxReadInterceptor : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public int SuppressedReads { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
                && command.CommandText.Contains("FROM \"RuntimeInbox\"", StringComparison.Ordinal))
            {
                // Force only the initial lookup to miss, as it can before a competing
                // commit. The subsequent INSERT hits the real SQLite unique constraint.
                command.CommandText = command.CommandText.Replace("WHERE ", "WHERE 0 = 1 AND ", StringComparison.Ordinal);
                Armed = false;
                SuppressedReads++;
            }
            return ValueTask.FromResult(result);
        }
    }
}
