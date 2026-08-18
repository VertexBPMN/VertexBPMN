using Moq;
using OpenTelemetry.Trace;
using Shouldly;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence.InMemory;

namespace VertexBPMN.Tests.Integration.Engine;

public sealed class DistributedProcessEngineTokenLifecycleTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly IProcessInstanceStore _store;

    private readonly DistributedProcessEngine _engine;

    public DistributedProcessEngineTokenLifecycleTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var loggerMock = new Mock<ILogger<DistributedProcessEngine>>();
        _store = new InMemoryProcessInstanceStore();
        var dispatcherMock = new Mock<IMessageDispatcher>();
        var cmmnParserMock = new Mock<ICmmnParser>();
        var aiDecisionServiceMock = new Mock<IAiDecisionService>();
        var tracerProvider = new Mock<TracerProvider>().Object;
        var registry = new ServiceTaskRegistry();
        var dmnParser = new Mock<IDmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();

        _engine = new DistributedProcessEngine(loggerMock.Object, registry, dispatcherMock.Object, _store, dmnEngine.Object, dmnParser.Object, cmmnParserMock.Object, bpmnParser.Object, aiDecisionServiceMock.Object, tracerProvider);

    }

    [Fact]
    public async Task EndEvent_SetsTokenToCompleted_AndPersistsIt()
    {
        var processInstanceId = Guid.NewGuid();

        var token = new ExecutionToken(
            id: Guid.NewGuid(),
            processInstanceId: processInstanceId,
            currentNodeId: "end",
            nodeType: "endEvent",
            variables: new Dictionary<string, object>(),
            createdAt: DateTime.UtcNow);

        token.SetState("Pending");

        await _store.SaveTokenAsync(token);

        var endEvent = new BpmnEvent(
            Id: "end",
            Type: "endEvent",
            Definitions: Array.Empty<EventDefinition>());

        var model = CreateEmptyModel();
        var trace = new List<string>();

        await InvokeProcessEventAsync(
            _engine,
            endEvent,
            token,
            model,
            trace);

        token.State.ShouldBe("Completed");

        var persistedToken = await _store.GetTokenAsync(token.Id);

        persistedToken.State.ShouldBe("Completed");
        trace.Count.ShouldBe(2);
        trace.ShouldContain(t => t.Contains("EndEvent:"));
        trace.ShouldContain(t => t.Contains("EndEventCompleted"));
    }

    [Fact]
    public async Task CompletedToken_IsNotProcessedAgain()
    {
        var token = new ExecutionToken(
            id: Guid.NewGuid(),
            processInstanceId: Guid.NewGuid(),
            currentNodeId: "end",
            nodeType: "endEvent",
            variables: new Dictionary<string, object>(),
            createdAt: DateTime.UtcNow);

        token.SetState("Completed");

        var trace = new List<string>();

        await InvokeProcessTokenAsync(
            _engine,
            token,
            CreateEmptyModel(),
            trace);

        trace.Count.ShouldBe(1);
        trace.Single().ShouldContain("TokenSkipped");
        trace.Single().ShouldContain("Completed");
    }

    [Fact]
    public async Task FailedToken_IsNotProcessedAgain()
    {
        
        

        var token = new ExecutionToken(
            id: Guid.NewGuid(),
            processInstanceId: Guid.NewGuid(),
            currentNodeId: "task",
            nodeType: "task",
            variables: new Dictionary<string, object>(),
            createdAt: DateTime.UtcNow);

        token.SetState("Failed");

        var trace = new List<string>();

        await InvokeProcessTokenAsync(
            _engine,
            token,
            CreateEmptyModel(),
            trace);

        trace.ShouldContain(
            entry => entry.Contains("TokenSkipped") &&
                     entry.Contains("Failed"));
    }

    [Fact]
    public async Task DeterministicModelFailure_IsNotRetried()
    {
        var token = new ExecutionToken(
            id: Guid.NewGuid(),
            processInstanceId: Guid.NewGuid(),
            currentNodeId: "missing-node",
            nodeType: "task",
            variables: new Dictionary<string, object>(),
            createdAt: DateTime.UtcNow);

        token.SetState("Pending");
        await _store.SaveTokenAsync(token);

        var trace = new List<string>();
        await InvokeProcessTokenAsync(_engine, token, CreateEmptyModel(), trace);

        token.State.ShouldBe("Failed");
        token.RetryCount.ShouldBe(1);
        trace.ShouldContain(entry => entry.Contains("TokenFailed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PendingToken_IsReturnedByInMemoryStore_WhenUnassigned()
    {
        var token = new ExecutionToken(
            id: Guid.NewGuid(),
            processInstanceId: Guid.NewGuid(),
            currentNodeId: "task",
            nodeType: "task",
            variables: new Dictionary<string, object>(),
            createdAt: DateTime.UtcNow);

        token.SetState("Pending");
        token.AssignedWorker = null;

        await _store.SaveTokenAsync(token);

        var pendingTokens =
            await _store.GetPendingTokensAsync();

        pendingTokens.Single().ShouldNotBeNull();
        pendingTokens.Single().Id.ShouldBe(token.Id);
    }


    private static BpmnModel CreateEmptyModel()
    {
        return new BpmnModel(
            "process",
            "process",
            Array.Empty<BpmnEvent>(),
            Array.Empty<BpmnGateway>(),
            Array.Empty<BpmnSubprocess>(),
            Array.Empty<BpmnSequenceFlow>(),
            Array.Empty<BpmnTask>());
    }

    private static async Task InvokeProcessEventAsync(
        DistributedProcessEngine engine,
        BpmnEvent evt,
        ExecutionToken token,
        BpmnModel model,
        List<string> trace)
    {
        var method = typeof(DistributedProcessEngine)
            .GetMethod(
                "ProcessEventAsync",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        method.ShouldNotBeNull();

        var result = method!.Invoke(
            engine,
            new object[]
            {
                evt,
                token,
                model,
                trace,
                CancellationToken.None
            });

        result.ShouldBeAssignableTo<Task>();

        await (Task)result!;
    }

    private static async Task InvokeProcessTokenAsync(
        DistributedProcessEngine engine,
        ExecutionToken token,
        BpmnModel model,
        List<string> trace)
    {
        var method = typeof(DistributedProcessEngine)
            .GetMethod(
                "ProcessTokenAsync",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        method.ShouldNotBeNull();

        var result = method!.Invoke(
            engine,
            new object[]
            {
                token,
                model,
                trace,
                CancellationToken.None
            });

        result.ShouldBeAssignableTo<Task>();

        await (Task)result!;
    }
}