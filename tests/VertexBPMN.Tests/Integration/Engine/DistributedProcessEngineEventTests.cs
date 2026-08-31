using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using Shouldly;
using System.Reflection;
using System.Runtime.ExceptionServices;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Engine;

public class DistributedProcessEngineEventTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Mock<ILogger<DistributedProcessEngine>> _loggerMock;
    private readonly Mock<IProcessInstanceStore> _storeMock;
    private readonly Mock<IMessageDispatcher> _dispatcherMock;
    private readonly Mock<ICmmnParser> _cmmnParserMock;
    private readonly Mock<IAiDecisionService> _aiDecisionServiceMock;

    private readonly DistributedProcessEngine _engine;

    public DistributedProcessEngineEventTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _loggerMock = new Mock<ILogger<DistributedProcessEngine>>();
        _storeMock = new Mock<IProcessInstanceStore>();
        _dispatcherMock = new Mock<IMessageDispatcher>();
        _cmmnParserMock = new Mock<ICmmnParser>();
        _aiDecisionServiceMock = new Mock<IAiDecisionService>();
        var tracerProvider = new Mock<TracerProvider>().Object;
        var registry = new ServiceTaskRegistry();
        var dmnParser = new Mock<IDmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        _engine = new DistributedProcessEngine(_loggerMock.Object, registry, _dispatcherMock.Object, _storeMock.Object, dmnEngine.Object, dmnParser.Object, _cmmnParserMock.Object, bpmnParser.Object, _aiDecisionServiceMock.Object, tracerProvider);

    }

    [Fact]
    public async Task NoneEndEvent_DoesNotContinueToNextNode()
    {
        var engine = CreateUninitializedEngine();
        var trace = new List<string>();

        var evt = new BpmnEvent(
            Id: "end",
            Type: "endEvent",
            Definitions: Array.Empty<EventDefinition>());

        await InvokeProcessEventAsync(
            engine,
            evt,
            CreateToken(),
            CreateModel(),
            trace);

        trace.ShouldContain("EndEvent: end");
        trace.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EventWithMoreThanOneDefinition_IsRejected()
    {
        var engine = CreateUninitializedEngine();
        var trace = new List<string>();

        var evt = new BpmnEvent(
            Id: "event",
            Type: "intermediateCatchEvent",
            Definitions: new EventDefinition[]
            {
                new TimerEventDefinition(
                    TimeDate: null,
                    TimeDuration: "PT10S",
                    TimeCycle: null),

                new MessageEventDefinition(
                    MessageRef: "message",
                    CorrelationKey: null)
            });

        var exception = await Should.ThrowAsync<DistributedTokenException>(
            () => InvokeProcessEventAsync(
                engine,
                evt,
                CreateToken(),
                CreateModel(),
                trace));

        exception.Message.ShouldContain(
            "contains 2 event definitions");

        trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task TimerEvent_IsRejectedInsteadOfUsingTaskDelay()
    {
        var engine = CreateUninitializedEngine();
        var trace = new List<string>();

        var evt = new BpmnEvent(
            Id: "timer",
            Type: "intermediateCatchEvent",
            Definitions: new EventDefinition[]
            {
                new TimerEventDefinition(
                    TimeDate: null,
                    TimeDuration: "PT10S",
                    TimeCycle: null)
            });

        var exception = await Should.ThrowAsync<DistributedTokenException>(
            () => InvokeProcessEventAsync(
                engine,
                evt,
                CreateToken(),
                CreateModel(),
                trace));

        exception.Message.ShouldContain(
            "Timer event 'timer' is not yet implemented with a persistent waiting state.");

        trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task MessageEvent_PersistsWaitingToken_AndRegistersSubscription()
    {
        var engine = CreateUninitializedEngine();
        var trace = new List<string>();
        var evt = new BpmnEvent(
            Id: "message",
            Type: "intermediateCatchEvent",
            Definitions: new EventDefinition[]
            {
                new MessageEventDefinition(
                    MessageRef: "order-approved",
                    CorrelationKey: "orderId")
            });
        var token = CreateToken();
        _storeMock.Setup(store => store.SaveTokenAsync(token)).Returns(Task.CompletedTask);
        _dispatcherMock.Setup(dispatcher => dispatcher.SubscribeToMessageAsync(
            "order-approved", It.IsAny<Func<Message, Task>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await InvokeProcessEventAsync(engine, evt, token, CreateModel(), trace);

        token.State.ShouldBe(ExecutionToken.WaitingState);
        trace.ShouldContain("MessageEventWaiting: message for message order-approved");
        _storeMock.Verify(store => store.SaveTokenAsync(token), Times.Once);
        _dispatcherMock.Verify(dispatcher => dispatcher.SubscribeToMessageAsync(
            "order-approved", It.IsAny<Func<Message, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DefinedStartEvent_IsNotTreatedAsNoneStartEvent()
    {
        var engine = CreateUninitializedEngine();
        var trace = new List<string>();

        var evt = new BpmnEvent(
            Id: "timer-start",
            Type: "startEvent",
            Definitions: new EventDefinition[]
            {
                new TimerEventDefinition(
                    TimeDate: null,
                    TimeDuration: "PT10S",
                    TimeCycle: null)
            });

        var exception = await Should.ThrowAsync<DistributedTokenException>(
            () => InvokeProcessEventAsync(
                engine,
                evt,
                CreateToken(),
                CreateModel(),
                trace));

        exception.Message.ShouldContain(
            "Timer event 'timer-start' is not yet implemented with a persistent waiting state.");

        trace.ShouldNotContain(
            "StartEvent: timer-start");
    }

    [Fact]
    public async Task UnknownEventType_IsRejected()
    {
        var engine = CreateUninitializedEngine();
        var trace = new List<string>();

        var evt = new BpmnEvent(
            Id: "unknown",
            Type: "unknownEvent",
            Definitions: Array.Empty<EventDefinition>());

        var exception = await Should.ThrowAsync<DistributedTokenException>(
            () => InvokeProcessEventAsync(
                engine,
                evt,
                CreateToken(),
                CreateModel(),
                trace));

        exception.Message.ShouldContain(
            "Unsupported BPMN event type");

        trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task BoundaryEventWithoutDefinition_IsRejected()
    {
        var engine = CreateUninitializedEngine();
        var trace = new List<string>();

        var evt = new BpmnEvent(
            Id: "boundary",
            Type: "boundaryEvent",
            Definitions: Array.Empty<EventDefinition>());

        var exception = await Should.ThrowAsync<DistributedTokenException>(
            () => InvokeProcessEventAsync(
                engine,
                evt,
                CreateToken(),
                CreateModel(),
                trace));

        exception.Message.ShouldContain(
            "has no event definition");

        trace.ShouldBeEmpty();
    }

    private  DistributedProcessEngine CreateUninitializedEngine()
    {
        return _engine;
    }

    private static ExecutionToken CreateToken()
    {
        return new ExecutionToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "node",
            "event");
    }

    private static BpmnModel CreateModel()
    {
        return new BpmnModel(
            ProcessId: "process",
            Name: "process");
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
                BindingFlags.Instance | BindingFlags.NonPublic);

        method.ShouldNotBeNull();

        try
        {
            var result = method!.Invoke(
                engine,
                new object[]
                {
                    evt,
                    token,
                    model,
                    trace,
                    TestContext.Current.CancellationToken
                });

            var task = Assert.IsAssignableFrom<Task>(result);
            await task;
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException != null)
        {
            ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();

            throw;
        }
    }
}
