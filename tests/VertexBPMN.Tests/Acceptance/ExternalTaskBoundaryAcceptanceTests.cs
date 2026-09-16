using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

public sealed class ExternalTaskBoundaryAcceptanceTests
{
    [Theory]
    [InlineData("Timer", true, 1)]
    [InlineData("Timer", false, 1)]
    [InlineData("Message", true, 1)]
    [InlineData("Message", false, 1)]
    [InlineData("Signal", true, 1)]
    [InlineData("Signal", false, 1)]
    [InlineData("Timer", true, 3)]
    [InlineData("Timer", false, 3)]
    [InlineData("Message", true, 3)]
    [InlineData("Message", false, 3)]
    [InlineData("Signal", true, 3)]
    [InlineData("Signal", false, 3)]
    public async Task BoundaryPreservesOrCancelsExternalWork(string kind, bool interrupting, int count)
    {
        var ct = TestContext.Current.CancellationToken;
        var eventDefinition = kind switch
        {
            "Timer" => "<timerEventDefinition><timeDuration>PT0S</timeDuration></timerEventDefinition>",
            "Message" => "<messageEventDefinition messageRef='message'/>",
            _ => "<signalEventDefinition signalRef='signal'/>"
        };
        var boundary = $"<boundaryEvent id='boundary' attachedToRef='work' cancelActivity='{interrupting.ToString().ToLowerInvariant()}'>{eventDefinition}</boundaryEvent><sequenceFlow id='escape' sourceRef='boundary' targetRef='e'/>";
        await using var fixture = await Fixture.CreateAsync(boundary, count, ct);
        var instance = await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic", ["unmapped"] = "private" }, null, "a", "start", ct);
        Assert.Equal(count, await fixture.Db.ExternalTaskJobs.CountAsync(ct));
        if (kind == "Timer")
        {
            var timer = await fixture.Db.Jobs.SingleAsync(ct);
            Assert.True(await fixture.Runtime.ExecuteJobAsync(timer.Id, "test-timer", ct));
            Assert.False(await fixture.Runtime.ExecuteJobAsync(timer.Id, "test-timer", ct));
        }
        else if (kind == "Message")
        {
            Assert.Single(await fixture.Db.EventSubscriptions.ToListAsync(ct));
            await fixture.Runtime.CorrelateMessageAsync("trigger", instance.Id, null, "a", "event", ct);
            await fixture.Runtime.CorrelateMessageAsync("trigger", instance.Id, null, "a", "event", ct);
        }
        else
        {
            Assert.Single(await fixture.Db.EventSubscriptions.ToListAsync(ct));
            await fixture.Runtime.BroadcastSignalAsync("trigger", null, "a", "event", ct);
            await fixture.Runtime.BroadcastSignalAsync("trigger", null, "a", "event", ct);
        }
        fixture.Db.ChangeTracker.Clear();
        var jobs = await fixture.Db.ExternalTaskJobs.ToListAsync(ct);
        Assert.All(jobs, job => Assert.Equal(interrupting ? ExternalTaskState.Cancelled : ExternalTaskState.Ready, job.State));
        var tokens = await fixture.Db.ExecutionTokens.Where(token => token.CurrentNodeId == "work").ToListAsync(ct);
        Assert.Equal(count, tokens.Count);
        Assert.All(tokens, token => Assert.Equal(interrupting ? ExecutionToken.CompletedState : ExecutionToken.WaitingState, token.State));
        var process = await fixture.Db.ProcessInstances.SingleAsync(ct);
        Assert.Equal(interrupting ? ProcessInstanceStatus.Completed : ProcessInstanceStatus.Running, process.Status);
        Assert.Equal(interrupting ? count : 0, await fixture.Db.HistoryEvents.CountAsync(item => item.EventType == "EXTERNAL_TASK_CANCELLED", ct));
        Assert.Equal(1, await fixture.Db.HistoryEvents.CountAsync(item => item.EventType == "END_EVENT_REACHED", ct));
        Assert.False(await fixture.Db.EventSubscriptions.AnyAsync(item => item.State == "Active", ct));
        if (interrupting) Assert.False(await fixture.Db.MultiInstanceExecutions.AnyAsync(item => item.State == "Active", ct));
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("missing", "external_task_input_missing")]
    [InlineData("schema", "external_task_input_schema_invalid")]
    [InlineData("large", "external_task_input_too_large")]
    public async Task InvalidInputCreatesRedactedIncidentWithoutAJob(string kind, string expectedCode)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        var variables = new Dictionary<string, object>();
        if (kind == "schema") variables["document"] = new { credentialRef = "must-not-leak" };
        if (kind == "large") variables["document"] = new string('x', 129 * 1024);
        var instance = await fixture.Runtime.StartAsync(fixture.Definition, variables, null, "a", "invalid", ct);
        Assert.Equal(ProcessInstanceStatus.Suspended, instance.Status);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(expectedCode, (await fixture.Db.Incidents.SingleAsync(ct)).Message);
        Assert.Empty(await fixture.Db.ExternalTaskJobs.ToListAsync(ct));
        Assert.Empty(await fixture.Db.ExecutionTokens.ToListAsync(ct));
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InterruptingBoundaryLoopCreatesNewExecutionAndRearmsSubscription()
    {
        var ct = TestContext.Current.CancellationToken;
        const string boundary = "<boundaryEvent id='boundary' attachedToRef='work'><messageEventDefinition messageRef='message'/></boundaryEvent><sequenceFlow id='again' sourceRef='boundary' targetRef='work'/>";
        await using var fixture = await Fixture.CreateAsync(boundary, 1, ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition, new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "start", ct);
        for (var i = 0; i < 2; i++)
            await fixture.Runtime.CorrelateMessageAsync("trigger", started.Id, null, "a", "repeat-" + i, ct);
        fixture.Db.ChangeTracker.Clear();
        var jobs = await fixture.Db.ExternalTaskJobs.ToListAsync(ct);
        Assert.Equal(3, jobs.Count);
        Assert.Equal(3, jobs.Select(item => item.ActivityExecutionId).Distinct().Count());
        Assert.Equal(2, jobs.Count(item => item.State == ExternalTaskState.Cancelled));
        Assert.Single(jobs, item => item.State == ExternalTaskState.Ready);
        Assert.Single(await fixture.Db.EventSubscriptions.Where(item => item.State == "Active").ToListAsync(ct));
        Assert.Single(await fixture.Db.ExecutionTokens.Where(item => item.CurrentNodeId == "work" && item.State == ExecutionToken.WaitingState).ToListAsync(ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmbeddedScopeKeepsIdentityAndBoundaryCancelsOnlyItsWork(bool attachToScope)
    {
        var ct = TestContext.Current.CancellationToken;
        var boundary = $"<boundaryEvent id='boundary' attachedToRef='{(attachToScope ? "scope" : "work")}'><messageEventDefinition messageRef='message'/></boundaryEvent><sequenceFlow id='escape' sourceRef='boundary' targetRef='{(attachToScope ? "e" : "innerEnd")}'/>";
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        var xml = fixture.Definition.BpmnXml;
        var taskStart = xml.IndexOf("<serviceTask", StringComparison.Ordinal);
        var taskEnd = xml.IndexOf("</serviceTask>", StringComparison.Ordinal) + "</serviceTask>".Length;
        var task = xml[taskStart..taskEnd];
        var scope = "<subProcess id='scope'><startEvent id='innerStart'/>" + task +
            "<endEvent id='innerEnd'/><sequenceFlow id='innerIn' sourceRef='innerStart' targetRef='work'/><sequenceFlow id='innerOut' sourceRef='work' targetRef='innerEnd'/>" +
            (attachToScope ? "" : boundary) + "</subProcess>";
        fixture.Definition.BpmnXml = xml.Replace(task, scope, StringComparison.Ordinal)
            .Replace("sourceRef=\"s\" targetRef=\"work\"", "sourceRef=\"s\" targetRef=\"scope\"", StringComparison.Ordinal)
            .Replace("sourceRef=\"work\" targetRef=\"e\"", "sourceRef=\"scope\" targetRef=\"e\"", StringComparison.Ordinal)
            .Replace("</process>", (attachToScope ? boundary : "") + "</process>", StringComparison.Ordinal);
        await fixture.Db.SaveChangesAsync(ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition, new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "start", ct);
        var job = await fixture.Db.ExternalTaskJobs.SingleAsync(ct);
        Assert.NotEqual(started.Id, job.ScopeExecutionId);
        Assert.NotEqual(Guid.Empty, job.ScopeExecutionId);
        Assert.Equal(job.ScopeExecutionId, (await fixture.Db.ExecutionTokens.SingleAsync(item => item.Id == job.WaitTokenId, ct)).ScopeExecutionId);
        await fixture.Runtime.CorrelateMessageAsync("trigger", started.Id, null, "a", "cancel", ct);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(ExternalTaskState.Cancelled, (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).State);
        Assert.Equal(ProcessInstanceStatus.Completed, (await fixture.Db.ProcessInstances.SingleAsync(ct)).Status);
        Assert.False(await fixture.Db.ExecutionTokens.AnyAsync(item => item.State == ExecutionToken.WaitingState, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultiInstanceUsesLocalInputsAndSequentialSchedulesOnlyFirstIteration(bool sequential)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        fixture.Definition.BpmnXml = fixture.Definition.BpmnXml.Replace("</serviceTask>",
            $"<multiInstanceLoopCharacteristics xmlns:c='http://camunda.org/schema/1.0/bpmn' isSequential='{sequential.ToString().ToLowerInvariant()}' c:collection='documents' c:elementVariable='document'/></serviceTask>", StringComparison.Ordinal);
        await fixture.Db.SaveChangesAsync(ct);
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "global", ["documents"] = new[] { "one", "two", "three" } }, null, "a", "start", ct);
        fixture.Db.ChangeTracker.Clear();
        var jobs = await fixture.Db.ExternalTaskJobs.OrderBy(item => item.MultiInstanceIndex).ToListAsync(ct);
        Assert.Equal(sequential ? 1 : 3, jobs.Count);
        var owner = await fixture.Db.MultiInstanceExecutions.SingleAsync(ct);
        Assert.Equal(3, owner.InstanceCount);
        Assert.Equal(0, owner.CompletedCount);
        Assert.Equal(sequential ? 1 : 3, owner.NextIndex);
        Assert.Equal(sequential, owner.IsSequential);
        for (var i = 0; i < jobs.Count; i++)
        {
            Assert.Equal(owner.Id, jobs[i].MultiInstanceExecutionId);
            Assert.Equal(i, jobs[i].MultiInstanceIndex);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(new { text = new[] { "one", "two", "three" }[i] }), jobs[i].InputSnapshot);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EventSubprocessSchedulesInItsOwnScopeAndHonorsInterruption(bool interrupting)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        var xml = fixture.Definition.BpmnXml;
        var taskStart = xml.IndexOf("<serviceTask", StringComparison.Ordinal);
        var taskEnd = xml.IndexOf("</serviceTask>", StringComparison.Ordinal) + "</serviceTask>".Length;
        var task = xml[taskStart..taskEnd].Replace("id=\"work\"", "id=\"eventWork\"", StringComparison.Ordinal);
        var eventScope = $"<subProcess id='events' triggeredByEvent='true'><startEvent id='eventStart' isInterrupting='{interrupting.ToString().ToLowerInvariant()}'><messageEventDefinition messageRef='message'/></startEvent>" + task +
            "<endEvent id='eventEnd'/><sequenceFlow id='eventIn' sourceRef='eventStart' targetRef='eventWork'/><sequenceFlow id='eventOut' sourceRef='eventWork' targetRef='eventEnd'/></subProcess>";
        fixture.Definition.BpmnXml = xml.Replace("</process>", eventScope + "</process>", StringComparison.Ordinal);
        await fixture.Db.SaveChangesAsync(ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition, new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "start", ct);
        await fixture.Runtime.CorrelateMessageAsync("trigger", started.Id, null, "a", "event", ct);
        fixture.Db.ChangeTracker.Clear();
        var main = await fixture.Db.ExternalTaskJobs.SingleAsync(item => item.ActivityId == "work", ct);
        var child = await fixture.Db.ExternalTaskJobs.SingleAsync(item => item.ActivityId == "eventWork", ct);
        Assert.Equal(interrupting ? ExternalTaskState.Cancelled : ExternalTaskState.Ready, main.State);
        Assert.Equal(ExternalTaskState.Ready, child.State);
        Assert.NotEqual(main.ScopeExecutionId, child.ScopeExecutionId);
        Assert.Equal(ProcessInstanceStatus.Running, (await fixture.Db.ProcessInstances.SingleAsync(ct)).Status);
    }

    [Fact]
    public async Task DisabledFeatureInCalledProcessIsDetectedBeforeEarlierServiceHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        var parent = new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = "parent", Name = "parent", TenantId = "a", TenantScope = "a", Version = 1,
            DeploymentId = fixture.Definition.DeploymentId,
            BpmnXml = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='parent'><startEvent id='s'/><serviceTask id='sideEffect'/><callActivity id='call' calledElement='p'/><sequenceFlow id='f1' sourceRef='s' targetRef='sideEffect'/><sequenceFlow id='f2' sourceRef='sideEffect' targetRef='call'/></process></definitions>"
        };
        fixture.Db.ProcessDefinitions.Add(parent);
        await fixture.Db.SaveChangesAsync(ct);
        var disabled = new PersistentProcessExecutionRuntime(fixture.Db, fixture.Registry.Object, Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await disabled.StartAsync(parent, null, null, "a", "start", ct));
        Assert.Equal("external_task_feature_not_enabled", error.Message);
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.ProcessInstances.ToListAsync(ct));
        Assert.Empty(await fixture.Db.ExternalTaskJobs.ToListAsync(ct));
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, "false", 0)]
    [InlineData(true, "true", 1)]
    [InlineData(false, "false", 1)]
    public async Task StandardLoopHonorsEntryCondition(bool testBefore, string condition, int expectedJobs)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        fixture.Definition.BpmnXml = fixture.Definition.BpmnXml.Replace("</serviceTask>",
            $"<standardLoopCharacteristics testBefore='{testBefore.ToString().ToLowerInvariant()}' loopMaximum='3'><loopCondition>{condition}</loopCondition></standardLoopCharacteristics></serviceTask>", StringComparison.Ordinal);
        await fixture.Db.SaveChangesAsync(ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition, new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "start", ct);
        Assert.Equal(expectedJobs, await fixture.Db.ExternalTaskJobs.CountAsync(ct));
        Assert.Equal(expectedJobs == 0 ? ProcessInstanceStatus.Completed : ProcessInstanceStatus.Running, started.Status);
        if (expectedJobs == 1)
            Assert.Equal("0", (await fixture.Db.ExecutionTokens.SingleAsync(ct)).Variables["loopCounter"].ToString());
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NestedIncidentRecoveryPreservesScopeAcrossReloadAndRepeatedFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        var xml = fixture.Definition.BpmnXml;
        var start = xml.IndexOf("<serviceTask", StringComparison.Ordinal);
        var end = xml.IndexOf("</serviceTask>", StringComparison.Ordinal) + "</serviceTask>".Length;
        var task = xml[start..end];
        fixture.Definition.BpmnXml = xml.Replace(task,
            "<subProcess id='scope'><startEvent id='innerStart'/>" + task +
            "<endEvent id='innerEnd'/><sequenceFlow id='innerIn' sourceRef='innerStart' targetRef='work'/>" +
            "<sequenceFlow id='innerOut' sourceRef='work' targetRef='innerEnd'/></subProcess>", StringComparison.Ordinal)
            .Replace("sourceRef=\"s\" targetRef=\"work\"", "sourceRef=\"s\" targetRef=\"scope\"", StringComparison.Ordinal)
            .Replace("sourceRef=\"work\" targetRef=\"e\"", "sourceRef=\"scope\" targetRef=\"e\"", StringComparison.Ordinal);
        await fixture.Db.SaveChangesAsync(ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition, null, null, "a", "start", ct);
        Assert.Equal(ProcessInstanceStatus.Suspended, started.Status);
        fixture.Db.ChangeTracker.Clear();
        var incident = await fixture.Db.Incidents.SingleAsync(ct);
        var failed = await fixture.Db.ExecutionTokens.SingleAsync(ct);
        var scopeId = Guid.Parse(failed.Variables["$vertex.scopeExecution.scope"].ToString()!);
        Assert.Equal(incident.Id, failed.Id);
        Assert.Equal(ExecutionToken.FailedState, failed.State);
        Assert.Empty(await fixture.Db.ExternalTaskJobs.ToListAsync(ct));

        await fixture.Runtime.RecoverIncidentAsync(incident.Id, "a", "retry-invalid", ct);
        fixture.Db.ChangeTracker.Clear();
        var open = await fixture.Db.Incidents.SingleAsync(item => item.State == "Open", ct);
        Assert.NotEqual(incident.Id, open.Id);
        Assert.Equal(scopeId.ToString(), (await fixture.Db.ExecutionTokens.SingleAsync(ct)).Variables["$vertex.scopeExecution.scope"].ToString());
        var instance = await fixture.Db.ProcessInstances.SingleAsync(ct);
        instance.Variables = new Dictionary<string, object>(instance.Variables) { ["document"] = "corrected" };
        await fixture.Db.SaveChangesAsync(ct);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Runtime.RecoverIncidentAsync(open.Id, "a", "retry-valid", ct);
        await fixture.Runtime.RecoverIncidentAsync(open.Id, "a", "retry-valid", ct);
        fixture.Db.ChangeTracker.Clear();
        var job = await fixture.Db.ExternalTaskJobs.SingleAsync(ct);
        Assert.Equal(scopeId, job.ScopeExecutionId);
        Assert.Equal(ProcessInstanceStatus.Running, (await fixture.Db.ProcessInstances.SingleAsync(ct)).Status);
        Assert.Equal(ExecutionToken.WaitingState, (await fixture.Db.ExecutionTokens.SingleAsync(ct)).State);
        Assert.All(await fixture.Db.Incidents.ToListAsync(ct), item => Assert.Equal("Resolved", item.State));
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublicSuspendPreservesExternalWaitAndResumeAllowsBoundaryCancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        const string boundary = "<boundaryEvent id='boundary' attachedToRef='work'><messageEventDefinition messageRef='message'/></boundaryEvent><sequenceFlow id='escape' sourceRef='boundary' targetRef='e'/>";
        await using var fixture = await Fixture.CreateAsync(boundary, 1, ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "start", ct);
        var service = new VertexBPMN.Application.RuntimeService(
            new VertexBPMN.Infrastructure.Persistence.Repositories.ProcessInstanceRepository(fixture.Db),
            Mock.Of<VertexBPMN.Domain.Interfaces.Repositories.IProcessDefinitionRepository>(),
            Mock.Of<IProcessMiningEventSink>(), fixture.Runtime);
        var before = await fixture.Db.ExternalTaskJobs.SingleAsync(ct);
        var jobId = before.Id;
        var waitId = before.WaitTokenId;
        await service.SuspendAsync(started.Id, ct);
        fixture.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CorrelateMessageAsync("trigger", started.Id.ToString(), cancellationToken: ct, tenantId: "a", idempotencyKey: "trigger"));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(ProcessInstanceStatus.Suspended, (await service.GetByIdAsync(started.Id, ct))!.Status);
        Assert.Equal(ExternalTaskState.Ready, (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).State);
        Assert.Equal(ExecutionToken.WaitingState, (await fixture.Db.ExecutionTokens.SingleAsync(t => t.Id == waitId, ct)).State);
        await service.ResumeAsync(started.Id, ct);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(jobId, (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).Id);
        Assert.Equal(waitId, (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).WaitTokenId);
        await service.CorrelateMessageAsync("trigger", started.Id.ToString(), cancellationToken: ct, tenantId: "a", idempotencyKey: "trigger");
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(ExternalTaskState.Cancelled, (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).State);
        Assert.Equal(ProcessInstanceStatus.Completed, (await service.GetByIdAsync(started.Id, ct))!.Status);
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublicResumeCannotBypassExternalTaskIncidentRecovery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition, null, null, "a", "start", ct);
        fixture.Db.ChangeTracker.Clear();
        var sink = new Mock<IProcessMiningEventSink>(MockBehavior.Strict);
        var service = new VertexBPMN.Application.RuntimeService(
            new VertexBPMN.Infrastructure.Persistence.Repositories.ProcessInstanceRepository(fixture.Db),
            Mock.Of<VertexBPMN.Domain.Interfaces.Repositories.IProcessDefinitionRepository>(), sink.Object, fixture.Runtime);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ResumeAsync(started.Id, ct));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(ProcessInstanceStatus.Suspended, (await service.GetByIdAsync(started.Id, ct))!.Status);
        Assert.Equal("Open", (await fixture.Db.Incidents.SingleAsync(ct)).State);
        Assert.Empty(await fixture.Db.ExternalTaskJobs.ToListAsync(ct));
        sink.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublicDeleteTerminatesAndRetainsExternalAuditState()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 3, ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "start", ct);
        fixture.Db.ChangeTracker.Clear();
        var service = new VertexBPMN.Application.RuntimeService(
            new VertexBPMN.Infrastructure.Persistence.Repositories.ProcessInstanceRepository(fixture.Db),
            Mock.Of<VertexBPMN.Domain.Interfaces.Repositories.IProcessDefinitionRepository>(),
            Mock.Of<IProcessMiningEventSink>(), fixture.Runtime);

        await service.DeleteAsync(started.Id, ct);
        await service.DeleteAsync(started.Id, ct);
        fixture.Db.ChangeTracker.Clear();

        var retained = await fixture.Db.ProcessInstances.SingleAsync(item => item.Id == started.Id, ct);
        Assert.Equal(ProcessInstanceStatus.Terminated, retained.Status);
        Assert.Equal("Terminated", retained.State);
        Assert.NotNull(retained.EndedAt);
        Assert.All(await fixture.Db.ExternalTaskJobs.ToListAsync(ct),
            item => Assert.Equal(ExternalTaskState.Cancelled, item.State));
        Assert.All(await fixture.Db.ExecutionTokens.ToListAsync(ct),
            item => Assert.NotEqual(ExecutionToken.WaitingState, item.State));
        Assert.All(await fixture.Db.MultiInstanceExecutions.ToListAsync(ct),
            item => Assert.Equal("Cancelled", item.State));
        Assert.Equal(1, await fixture.Db.HistoryEvents.CountAsync(
            item => item.ProcessInstanceId == started.Id && item.EventType == "PROCESS_TERMINATED", ct));
        Assert.Equal(3, await fixture.Db.HistoryEvents.CountAsync(
            item => item.ProcessInstanceId == started.Id && item.EventType == "EXTERNAL_TASK_CANCELLED", ct));
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublicDeleteClosesExternalIncidentRecoveryState()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        var xml = fixture.Definition.BpmnXml;
        var start = xml.IndexOf("<serviceTask", StringComparison.Ordinal);
        var end = xml.IndexOf("</serviceTask>", StringComparison.Ordinal) + "</serviceTask>".Length;
        var task = xml[start..end];
        fixture.Definition.BpmnXml = xml.Replace(task,
            "<subProcess id='scope'><startEvent id='innerStart'/>" + task +
            "<endEvent id='innerEnd'/><sequenceFlow id='innerIn' sourceRef='innerStart' targetRef='work'/>" +
            "<sequenceFlow id='innerOut' sourceRef='work' targetRef='innerEnd'/></subProcess>", StringComparison.Ordinal)
            .Replace("sourceRef=\"s\" targetRef=\"work\"", "sourceRef=\"s\" targetRef=\"scope\"", StringComparison.Ordinal)
            .Replace("sourceRef=\"work\" targetRef=\"e\"", "sourceRef=\"scope\" targetRef=\"e\"", StringComparison.Ordinal);
        await fixture.Db.SaveChangesAsync(ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition, null, null, "a", "start", ct);
        fixture.Db.ChangeTracker.Clear();
        await fixture.Runtime.TerminateAsync(started.Id, "a", ct);
        await fixture.Runtime.TerminateAsync(started.Id, "a", ct);
        fixture.Db.ChangeTracker.Clear();

        Assert.Equal(ProcessInstanceStatus.Terminated,
            (await fixture.Db.ProcessInstances.SingleAsync(ct)).Status);
        Assert.Equal(ExecutionToken.CompletedState,
            (await fixture.Db.ExecutionTokens.SingleAsync(ct)).State);
        Assert.Equal("Resolved", (await fixture.Db.Incidents.SingleAsync(ct)).State);
        Assert.Equal(1, await fixture.Db.HistoryEvents.CountAsync(item => item.EventType == "PROCESS_TERMINATED", ct));
        Assert.Empty(await fixture.Db.ExternalTaskJobs.ToListAsync(ct));
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ParallelMultiInstanceIncidentsRecoverTheirOwnExecutionsExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 3, ct);
        var started = await fixture.Runtime.StartAsync(fixture.Definition, null, null, "a", "start", ct);
        Assert.Equal(ProcessInstanceStatus.Suspended, started.Status);
        fixture.Db.ChangeTracker.Clear();
        var incidents = await fixture.Db.Incidents.OrderBy(item => item.Id).ToListAsync(ct);
        var failedTokens = await fixture.Db.ExecutionTokens.OrderBy(item => item.Id).ToListAsync(ct);
        Assert.Equal(3, incidents.Count);
        Assert.Equal(3, failedTokens.Count);
        Assert.Equal(incidents.Select(item => item.Id).Order(), failedTokens.Select(item => item.Id).Order());
        var executionIds = failedTokens.Select(token =>
            Guid.Parse(token.Variables["$vertex.multiInstanceId"].ToString()!)).ToHashSet();
        var executionId = Assert.Single(executionIds);
        Assert.Equal([0, 1, 2], failedTokens.Select(token =>
            int.Parse(token.Variables["$vertex.multiInstanceIndex"].ToString()!,
                System.Globalization.CultureInfo.InvariantCulture)).Order());

        var instance = await fixture.Db.ProcessInstances.SingleAsync(ct);
        instance.Variables = new Dictionary<string, object>(instance.Variables) { ["document"] = "corrected" };
        await fixture.Db.SaveChangesAsync(ct);
        fixture.Db.ChangeTracker.Clear();
        foreach (var incident in incidents)
        {
            var key = $"recover-{incident.Id:N}";
            await fixture.Runtime.RecoverIncidentAsync(incident.Id, "a", key, ct);
            await fixture.Runtime.RecoverIncidentAsync(incident.Id, "a", key, ct);
            fixture.Db.ChangeTracker.Clear();
        }

        var jobs = await fixture.Db.ExternalTaskJobs.ToListAsync(ct);
        Assert.Equal(3, jobs.Count);
        Assert.All(jobs, item => Assert.Equal(executionId, item.MultiInstanceExecutionId));
        Assert.Equal([0, 1, 2], jobs.Select(item => item.MultiInstanceIndex!.Value).Order());
        Assert.Equal(3, jobs.Select(item => item.ActivityExecutionId).Distinct().Count());
        Assert.Equal(3, jobs.Select(item => item.WaitTokenId).Distinct().Count());
        Assert.All(await fixture.Db.Incidents.ToListAsync(ct), item => Assert.Equal("Resolved", item.State));
        Assert.Equal(ProcessInstanceStatus.Running,
            (await fixture.Db.ProcessInstances.SingleAsync(ct)).Status);
        fixture.Registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublicDeleteStillPhysicallyDeletesProcessWithoutExternalAuditData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync("", 1, ct);
        var plain = new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = "plain", Name = "plain", TenantId = "a", TenantScope = "a", Version = 1,
            DeploymentId = fixture.Definition.DeploymentId,
            BpmnXml = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='plain'>" +
                "<startEvent id='s'/><userTask id='approval' name='Approval'/><endEvent id='e'/>" +
                "<sequenceFlow id='f1' sourceRef='s' targetRef='approval'/><sequenceFlow id='f2' sourceRef='approval' targetRef='e'/>" +
                "</process></definitions>"
        };
        fixture.Db.ProcessDefinitions.Add(plain);
        await fixture.Db.SaveChangesAsync(ct);
        var started = await fixture.Runtime.StartAsync(plain, null, null, "a", "plain-start", ct);
        var service = new VertexBPMN.Application.RuntimeService(
            new VertexBPMN.Infrastructure.Persistence.Repositories.ProcessInstanceRepository(fixture.Db),
            Mock.Of<VertexBPMN.Domain.Interfaces.Repositories.IProcessDefinitionRepository>(),
            Mock.Of<IProcessMiningEventSink>(), fixture.Runtime);

        await service.DeleteAsync(started.Id, ct);
        fixture.Db.ChangeTracker.Clear();

        Assert.False(await fixture.Db.ProcessInstances.AnyAsync(item => item.Id == started.Id, ct));
        Assert.False(await fixture.Db.Tasks.AnyAsync(item => item.ProcessInstanceId == started.Id, ct));
        Assert.False(await fixture.Db.ExternalTaskJobs.AnyAsync(item => item.ProcessInstanceId == started.Id, ct));
        fixture.Registry.VerifyNoOtherCalls();
    }

    private sealed class Fixture(SqliteConnection connection, BpmnDbContext db, ProcessDefinition definition,
        Mock<IServiceTaskRegistry> registry, PersistentProcessExecutionRuntime runtime) : IAsyncDisposable
    {
        public BpmnDbContext Db => db;
        public ProcessDefinition Definition => definition;
        public Mock<IServiceTaskRegistry> Registry => registry;
        public PersistentProcessExecutionRuntime Runtime => runtime;
        public static async Task<Fixture> CreateAsync(string boundary, int count, CancellationToken ct)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(ct);
            var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "boundary", TenantId = "a" };
                var loop = count == 1 ? "" : $"<multiInstanceLoopCharacteristics isSequential='false'><loopCardinality>{count}</loopCardinality></multiInstanceLoopCharacteristics>";
                var definition = new ProcessDefinition
                {
                    Id = Guid.NewGuid(), Key = "p", Name = "boundary", TenantId = "a", TenantScope = "a", Version = 1, DeploymentId = deployment.Id,
                    BpmnXml = $$"""
                    <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:v="https://vertexbpmn.io/schema/bpmn/1.0">
                    <message id="message" name="trigger"/><signal id="signal" name="trigger"/>
                    <process id="p"><startEvent id="s"/><serviceTask id="work"><extensionElements>
                    <v:externalTask topic="test.work" maxRetries="0" deadlineSeconds="300"/>
                    <v:ioMapping><v:input name="text" expression="document"/></v:ioMapping>
                    </extensionElements>{{loop}}</serviceTask><endEvent id="e"/>
                    <sequenceFlow id="f1" sourceRef="s" targetRef="work"/><sequenceFlow id="f2" sourceRef="work" targetRef="e"/>
                    {{boundary}}</process></definitions>
                    """
                };
                db.EngineDeployments.Add(deployment);
                db.ProcessDefinitions.Add(definition);
                await db.SaveChangesAsync(ct);
                var registry = new Mock<IServiceTaskRegistry>(MockBehavior.Strict);
                var config = Unit.Infrastructure.ExternalTaskContractResolverTests.Configuration();
                var runtime = new PersistentProcessExecutionRuntime(db, registry.Object, Mock.Of<IDecisionService>(),
                    NullLogger<PersistentProcessExecutionRuntime>.Instance, config, new ConfiguredExternalTaskContractResolver(config));
                return new Fixture(connection, db, definition, registry, runtime);
            }
            catch
            {
                await db.DisposeAsync();
                await connection.DisposeAsync();
                throw;
            }
        }
        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
