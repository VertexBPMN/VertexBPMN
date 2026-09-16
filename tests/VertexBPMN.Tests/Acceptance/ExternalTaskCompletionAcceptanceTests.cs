using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

public sealed class ExternalTaskCompletionAcceptanceTests
{
    [Fact]
    public async Task ContractReviewResultAlwaysContinuesToHumanReviewTask()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(ContractReviewProcess(), ct, ConfigureContractReview);
        var instance = await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "Synthetic clause", ["documentId"] = "doc-1", ["documentVersion"] = "v1" },
            null, "a", "agent-review", ct);
        fixture.Db.ChangeTracker.Clear();
        var worker = new ExternalTaskWorkerContext("issuer", "worker", "a",
            new HashSet<string>(["agent.contract-review"], StringComparer.Ordinal),
            new HashSet<string>(["contract-reviewer.v1"], StringComparer.Ordinal));
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["agent.contract-review"]), ct));
        using var result = JsonDocument.Parse("""
            {"schemaVersion":"contract-review.v1","documentVersion":"v1","summary":"Review summary","findings":"[]","uncertainties":"[]","requiresHumanReview":true,"promptVersion":"contract-reviewer.prompt.v1","documentHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}
            """);
        await fixture.Leases.CompleteAsync(worker, lease.JobId,
            new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                result.RootElement.Clone()), ct);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("human-review", (await fixture.Db.Tasks.SingleAsync(ct)).ActivityId);
        var stored = await fixture.Db.ProcessInstances.SingleAsync(item => item.Id == instance.Id, ct);
        var review = Assert.IsType<JsonElement>(stored.Variables["contractReview"]);
        Assert.True(review.GetProperty("requiresHumanReview").GetBoolean());
        Assert.Equal(ProcessInstanceStatus.Running, stored.Status);
    }

    [Fact]
    public async Task AcceptedResultIsAppliedOnceAndContinuesInSeparateDispatch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(SuccessProcess(), ct);
        var instance = await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "start", ct);
        fixture.Db.ChangeTracker.Clear();
        var worker = Worker();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"]), ct));
        using var result = JsonDocument.Parse("{\"approved\":true}");
        var completionId = Guid.NewGuid();
        await fixture.Leases.CompleteAsync(worker, lease.JobId,
            new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, completionId,
                result.RootElement.Clone()), ct);
        var completedJob = await fixture.Db.ExternalTaskJobs.SingleAsync(item => item.Id == lease.JobId, ct);
        Assert.Contains("vertex:ioMapping.output.result", completedJob.DefinitionSnapshot, StringComparison.Ordinal);
        fixture.Db.ChangeTracker.Clear();

        var processed = await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);

        Assert.Equal(1, processed.ContinuationsApplied);
        Assert.Equal(1, processed.DispatchesCompleted);
        Assert.Equal(0, processed.Conflicts);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.ProcessInstances.SingleAsync(item => item.Id == instance.Id, ct);
        var tokens = await fixture.Db.ExecutionTokens.AsNoTracking().ToListAsync(ct);
        Assert.True(stored.Variables.TryGetValue("review", out var review),
            $"Expected root output; variables={JsonSerializer.Serialize(stored.Variables)}, " +
            $"tokens={JsonSerializer.Serialize(tokens.Select(item => item.Variables))}, " +
            $"multiInstanceExecutionId={completedJob.MultiInstanceExecutionId}, " +
            $"definitionSnapshot={completedJob.DefinitionSnapshot}");
        Assert.Equal("{\"approved\":true}", JsonSerializer.Serialize(review));
        Assert.Equal("after", (await fixture.Db.Tasks.SingleAsync(ct)).ActivityId);
        Assert.Equal(ExternalTaskContinuationState.Applied,
            (await fixture.Db.ExternalTaskContinuations.SingleAsync(ct)).State);
        var storedJob = await fixture.Db.ExternalTaskJobs.SingleAsync(ct);
        Assert.Equal(ExecutionToken.CompletedState,
            (await fixture.Db.ExecutionTokens.SingleAsync(item => item.Id == storedJob.WaitTokenId, ct)).State);
        Assert.Equal(new ExternalTaskContinuationBatchResult(0, 0, 0),
            await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct));
        fixture.Db.ChangeTracker.Clear();
        var receipt = await fixture.Leases.CompleteAsync(worker, lease.JobId,
            new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, completionId,
                result.RootElement.Clone()), ct);
        Assert.Equal("Applied", receipt.ContinuationState);
    }

    [Fact]
    public async Task AllowedBusinessErrorUsesMatchingBoundaryWithoutSuccessOutput()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(BusinessErrorProcess(), ct);
        var instance = await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "start", ct);
        fixture.Db.ChangeTracker.Clear();
        var worker = Worker();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"]), ct));
        await fixture.Leases.FailAsync(worker, lease.JobId,
            new ExternalTaskFailCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                "business", "contract_rejected"), ct);
        fixture.Db.ChangeTracker.Clear();

        var processed = await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);

        Assert.Equal(1, processed.ContinuationsApplied);
        Assert.Equal(1, processed.DispatchesCompleted);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("rejected", (await fixture.Db.Tasks.SingleAsync(ct)).ActivityId);
        var stored = await fixture.Db.ProcessInstances.SingleAsync(item => item.Id == instance.Id, ct);
        Assert.False(stored.Variables.ContainsKey("review"));
        Assert.Equal(ProcessInstanceStatus.Running, stored.Status);
        Assert.Empty(await fixture.Db.Incidents.ToListAsync(ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MultiInstanceCompletesEveryExternalIterationBeforeContinuing(bool sequential)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(SuccessProcess().Replace("</extensionElements></serviceTask>",
            "</extensionElements><multiInstanceLoopCharacteristics xmlns:c='http://camunda.org/schema/1.0/bpmn' " +
            $"isSequential='{sequential.ToString().ToLowerInvariant()}' c:collection='documents' c:elementVariable='document'/></serviceTask>",
            StringComparison.Ordinal), ct);
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "root", ["documents"] = new[] { "one", "two", "three" } },
            null, "a", "mi-start", ct);
        Assert.Equal(3, (await fixture.Db.MultiInstanceExecutions.SingleAsync(ct)).InstanceCount);
        Assert.Equal(sequential ? 1 : 3,
            await fixture.Db.ExternalTaskJobs.CountAsync(item => item.State == ExternalTaskState.Ready, ct));

        for (var index = 0; index < 3; index++)
        {
            fixture.Db.ChangeTracker.Clear();
            var claimed = await fixture.Leases.ClaimAsync(Worker(),
                new ExternalTaskClaimCommand(["test.work"]), ct);
            Assert.True(claimed.Count == 1,
                $"Iteration {index} expected one lease; jobs=" + JsonSerializer.Serialize(
                    await fixture.Db.ExternalTaskJobs.AsNoTracking().Select(item => new
                    { item.State, item.MultiInstanceIndex, item.AvailableAt, item.Deadline }).ToListAsync(ct)));
            var lease = claimed[0];
            using var result = JsonDocument.Parse("{\"approved\":true}");
            await fixture.Leases.CompleteAsync(Worker(), lease.JobId,
                new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                    result.RootElement.Clone()), ct);
            fixture.Db.ChangeTracker.Clear();
            var processed = await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);
            Assert.Equal(1, processed.ContinuationsApplied);
        }

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(3, await fixture.Db.ExternalTaskJobs.CountAsync(item => item.State == ExternalTaskState.Completed, ct));
        Assert.Equal("Completed", (await fixture.Db.MultiInstanceExecutions.SingleAsync(ct)).State);
        Assert.Equal("after", (await fixture.Db.Tasks.SingleAsync(ct)).ActivityId);
    }

    [Fact]
    public async Task StandardLoopCreatesAndCompletesExactlyThreeExternalExecutions()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(SuccessProcess().Replace("</extensionElements></serviceTask>",
            "</extensionElements><standardLoopCharacteristics testBefore='false' loopMaximum='3'>" +
            "<loopCondition>true</loopCondition></standardLoopCharacteristics></serviceTask>",
            StringComparison.Ordinal), ct);
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "loop-start", ct);

        for (var index = 0; index < 3; index++)
        {
            fixture.Db.ChangeTracker.Clear();
            var lease = Assert.Single(await fixture.Leases.ClaimAsync(Worker(),
                new ExternalTaskClaimCommand(["test.work"]), ct));
            using var result = JsonDocument.Parse("{\"approved\":true}");
            await fixture.Leases.CompleteAsync(Worker(), lease.JobId,
                new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                    result.RootElement.Clone()), ct);
            fixture.Db.ChangeTracker.Clear();
            await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);
        }

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(3, await fixture.Db.ExternalTaskJobs.CountAsync(item => item.State == ExternalTaskState.Completed, ct));
        Assert.Equal("after", (await fixture.Db.Tasks.SingleAsync(ct)).ActivityId);
    }

    [Fact]
    public async Task TerminalTechnicalFailureSuspendsWithRedactedIncident()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(SuccessProcess(), ct);
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "failure-start", ct);
        fixture.Db.ChangeTracker.Clear();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(Worker(),
            new ExternalTaskClaimCommand(["test.work"]), ct));
        await fixture.Leases.FailAsync(Worker(), lease.JobId,
            new ExternalTaskFailCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                "technical", "invalid_input"), ct);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(ProcessInstanceStatus.Suspended,
            (await fixture.Db.ProcessInstances.SingleAsync(ct)).Status);
        Assert.Equal("external_task_failed", (await fixture.Db.Incidents.SingleAsync(ct)).Message);
        Assert.False((await fixture.Db.ProcessInstances.SingleAsync(ct)).Variables.ContainsKey("review"));
    }

    [Fact]
    public async Task InterruptingTimerWinsBeforeCompletionAndRejectsLateResult()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(TimerRaceProcess(), ct);
        var instance = await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "timer-first", ct);
        fixture.Db.ChangeTracker.Clear();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(Worker(),
            new ExternalTaskClaimCommand(["test.work"]), ct));
        var timer = await fixture.Db.Jobs.SingleAsync(ct);
        Assert.True(await fixture.Runtime.ExecuteJobAsync(timer.Id, "timer", ct));
        using var result = JsonDocument.Parse("{\"approved\":true}");

        var rejected = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await fixture.Leases.CompleteAsync(Worker(), lease.JobId,
                new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                    result.RootElement.Clone()), ct));

        Assert.Equal("external_task_not_found", rejected.Code);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("timed", (await fixture.Db.Tasks.SingleAsync(ct)).ActivityId);
        Assert.False((await fixture.Db.ProcessInstances.SingleAsync(item => item.Id == instance.Id, ct))
            .Variables.ContainsKey("review"));
    }

    [Fact]
    public async Task CompletionWinsBeforeInterruptingTimerAndCancelsTimer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(TimerRaceProcess(), ct);
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "completion-first", ct);
        fixture.Db.ChangeTracker.Clear();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(Worker(),
            new ExternalTaskClaimCommand(["test.work"]), ct));
        var timerId = (await fixture.Db.Jobs.SingleAsync(ct)).Id;
        using var result = JsonDocument.Parse("{\"approved\":true}");
        await fixture.Leases.CompleteAsync(Worker(), lease.JobId,
            new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                result.RootElement.Clone()), ct);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);

        fixture.Db.ChangeTracker.Clear();
        Assert.False(await fixture.Runtime.ExecuteJobAsync(timerId, "timer", ct));
        Assert.Equal("after", (await fixture.Db.Tasks.SingleAsync(ct)).ActivityId);
        Assert.True((await fixture.Db.ProcessInstances.SingleAsync(ct)).Variables.ContainsKey("review"));
    }

    [Fact]
    public async Task TerminationAfterReceiptCancelsPendingContinuationWithoutApplyingOutput()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(SuccessProcess(), ct);
        var instance = await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "terminate-after-receipt", ct);
        fixture.Db.ChangeTracker.Clear();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(Worker(),
            new ExternalTaskClaimCommand(["test.work"]), ct));
        using var result = JsonDocument.Parse("{\"approved\":true}");
        await fixture.Leases.CompleteAsync(Worker(), lease.JobId,
            new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                result.RootElement.Clone()), ct);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Runtime.TerminateAsync(instance.Id, "a", ct);
        var processed = await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(new ExternalTaskContinuationBatchResult(0, 0, 0), processed);
        Assert.Equal(ExternalTaskState.Completed, (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).State);
        Assert.Equal(ExternalTaskContinuationState.Cancelled,
            (await fixture.Db.ExternalTaskContinuations.SingleAsync(ct)).State);
        var stored = await fixture.Db.ProcessInstances.SingleAsync(ct);
        Assert.Equal(ProcessInstanceStatus.Terminated, stored.Status);
        Assert.False(stored.Variables.ContainsKey("review"));
    }

    private static ExternalTaskWorkerContext Worker() => new("issuer", "worker", "a",
        new HashSet<string>(["test.work"], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    private static string SuccessProcess() => """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
          <process id="p"><startEvent id="s"/><serviceTask id="work"><extensionElements>
            <vertex:externalTask topic="test.work" maxRetries="0" deadlineSeconds="300"/>
            <vertex:ioMapping><vertex:input name="text" expression="document"/><vertex:output name="result" target="review"/></vertex:ioMapping>
          </extensionElements></serviceTask><userTask id="after"/><endEvent id="e"/>
          <sequenceFlow id="f1" sourceRef="s" targetRef="work"/><sequenceFlow id="f2" sourceRef="work" targetRef="after"/>
          <sequenceFlow id="f3" sourceRef="after" targetRef="e"/></process></definitions>
        """;

    private static string BusinessErrorProcess() => """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
          <error id="contract_rejected" errorCode="contract_rejected"/>
          <process id="p"><startEvent id="s"/><serviceTask id="work"><extensionElements>
            <vertex:externalTask topic="test.work" maxRetries="0" deadlineSeconds="300"/>
            <vertex:ioMapping><vertex:input name="text" expression="document"/></vertex:ioMapping>
          </extensionElements></serviceTask><boundaryEvent id="onReject" attachedToRef="work">
            <errorEventDefinition errorRef="contract_rejected"/></boundaryEvent><userTask id="rejected"/>
          <endEvent id="success"/><sequenceFlow id="f1" sourceRef="s" targetRef="work"/>
          <sequenceFlow id="f2" sourceRef="work" targetRef="success"/><sequenceFlow id="f3" sourceRef="onReject" targetRef="rejected"/>
          </process></definitions>
        """;

    private static string TimerRaceProcess() => """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
          <process id="p"><startEvent id="s"/><serviceTask id="work"><extensionElements>
            <vertex:externalTask topic="test.work" maxRetries="0" deadlineSeconds="300"/>
            <vertex:ioMapping><vertex:input name="text" expression="document"/><vertex:output name="result" target="review"/></vertex:ioMapping>
          </extensionElements></serviceTask><boundaryEvent id="timeout" attachedToRef="work">
            <timerEventDefinition><timeDuration>PT0S</timeDuration></timerEventDefinition></boundaryEvent>
          <userTask id="after"/><userTask id="timed"/>
          <sequenceFlow id="f1" sourceRef="s" targetRef="work"/><sequenceFlow id="f2" sourceRef="work" targetRef="after"/>
          <sequenceFlow id="f3" sourceRef="timeout" targetRef="timed"/></process></definitions>
        """;

    private static string ContractReviewProcess() => """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
          <process id="p"><startEvent id="s"/><serviceTask id="agent-review"><extensionElements>
            <vertex:externalTask topic="agent.contract-review" agentProfileRef="contract-reviewer.v1" maxRetries="1" deadlineSeconds="300"/>
            <vertex:ioMapping><vertex:input name="document" expression="document"/><vertex:input name="documentId" expression="documentId"/><vertex:input name="documentVersion" expression="documentVersion"/><vertex:output name="result" target="contractReview"/></vertex:ioMapping>
          </extensionElements></serviceTask><userTask id="human-review" name="Human contract review"/><endEvent id="e"/>
          <sequenceFlow id="f1" sourceRef="s" targetRef="agent-review"/><sequenceFlow id="f2" sourceRef="agent-review" targetRef="human-review"/>
          <sequenceFlow id="f3" sourceRef="human-review" targetRef="e"/></process></definitions>
        """;

    private static void ConfigureContractReview(Microsoft.Extensions.Configuration.IConfigurationRoot configuration)
    {
        configuration["ExternalTasks:Contracts:0:Topic"] = "agent.contract-review";
        configuration["ExternalTasks:Contracts:0:AgentProfileRef"] = "contract-reviewer.v1";
        configuration["ExternalTasks:Contracts:0:AgentProfileVersion"] = "contract-reviewer.v1";
        configuration["ExternalTasks:Contracts:0:MaxAttempts"] = "2";
        configuration["ExternalTasks:Contracts:0:Inputs:0:Name"] = "document";
        configuration["ExternalTasks:Contracts:0:Inputs:0:MaxLength"] = "65536";
        configuration["ExternalTasks:Contracts:0:Inputs:1:Name"] = "documentId";
        configuration["ExternalTasks:Contracts:0:Inputs:1:MaxLength"] = "256";
        configuration["ExternalTasks:Contracts:0:Inputs:1:AllowExternalTransfer"] = "true";
        configuration["ExternalTasks:Contracts:0:Inputs:2:Name"] = "documentVersion";
        configuration["ExternalTasks:Contracts:0:Inputs:2:MaxLength"] = "256";
        configuration["ExternalTasks:Contracts:0:Inputs:2:AllowExternalTransfer"] = "true";
        var outputs = new (string Name, string Type, int MaxLength)[]
        {
            ("schemaVersion", "string", 64), ("documentVersion", "string", 256),
            ("summary", "string", 4000), ("findings", "string", 12000),
            ("uncertainties", "string", 6000), ("requiresHumanReview", "boolean", 1),
            ("promptVersion", "string", 128), ("documentHash", "string", 64)
        };
        for (var index = 0; index < outputs.Length; index++)
        {
            configuration[$"ExternalTasks:Contracts:0:Outputs:{index}:Name"] = outputs[index].Name;
            configuration[$"ExternalTasks:Contracts:0:Outputs:{index}:Type"] = outputs[index].Type;
            configuration[$"ExternalTasks:Contracts:0:Outputs:{index}:MaxLength"] = outputs[index].MaxLength.ToString();
            configuration[$"ExternalTasks:Contracts:0:Outputs:{index}:AllowExternalTransfer"] = "true";
        }
    }

    private sealed class Fixture(SqliteConnection connection, BpmnDbContext db, ProcessDefinition definition,
        PersistentProcessExecutionRuntime runtime, ExternalTaskLeaseService leases) : IAsyncDisposable
    {
        public BpmnDbContext Db => db;
        public ProcessDefinition Definition => definition;
        public PersistentProcessExecutionRuntime Runtime => runtime;
        public ExternalTaskLeaseService Leases => leases;

        public static async Task<Fixture> CreateAsync(string xml, CancellationToken ct,
            Action<Microsoft.Extensions.Configuration.IConfigurationRoot>? configure = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync(ct);
            var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(ct);
            var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "completion", TenantId = "a" };
            var definition = new ProcessDefinition
            {
                Id = Guid.NewGuid(), DeploymentId = deployment.Id, Key = "p", Name = "completion",
                TenantId = "a", TenantScope = "a", Version = 1, BpmnXml = xml
            };
            db.AddRange(deployment, definition);
            await db.SaveChangesAsync(ct);
            var configuration = VertexBPMN.Tests.Unit.Infrastructure.ExternalTaskContractResolverTests.Configuration();
            configure?.Invoke(configuration);
            var contracts = new ConfiguredExternalTaskContractResolver(configuration);
            var runtime = new PersistentProcessExecutionRuntime(db, Mock.Of<IServiceTaskRegistry>(),
                Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance,
                configuration, contracts);
            return new Fixture(connection, db, definition, runtime,
                new ExternalTaskLeaseService(db, contracts, configuration: configuration));
        }

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
