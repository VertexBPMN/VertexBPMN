using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using Shouldly;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Application;
using VertexBPMN.Application.Messaging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Engine.Configuration;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Infrastructure.Persistence.InMemory;
using ExecutionToken = VertexBPMN.Domain.Entities.ExecutionToken;

namespace VertexBPMN.Tests.Integration.Engine;

public class DistributedProcessEngineTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Mock<ILogger<DistributedProcessEngine>> _loggerMock;
    private readonly Mock<IProcessInstanceStore> _storeMock;
    private readonly Mock<IMessageDispatcher> _dispatcherMock;
    private readonly Mock<ICmmnParser> _cmmnParserMock;
    private readonly Mock<IAiDecisionService> _aiDecisionServiceMock;

    private readonly DistributedProcessEngine _engine;

    public DistributedProcessEngineTests(ITestOutputHelper testOutputHelper)
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
    public async Task TriggerUserEventAsync_TriggersEventListener_Successfully()
    {
        // Arrange
        var caseId = "D2DD968A-6748-4C17-83A6-8EAE354F5C77";
        var eventId = "event1";
        var caseModel = new CaseModel(
            caseId,
            "Test Case",
            [
                new PlanItem(eventId, "eventListener", "userEventListener", null, null)
            ],
            [],
            []
        );
        var caseToken = new CaseToken(Guid.NewGuid(), Guid.Parse(caseId), eventId, "eventListener", new Dictionary<string, object>(), DateTime.UtcNow);

        _storeMock.Setup(s => s.GetCmmnModelAsync(caseId)).ReturnsAsync("<cmmn:case id='D2DD968A-6748-4C17-83A6-8EAE354F5C77'>...</cmmn:case>");
        _cmmnParserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(caseModel);
        _storeMock.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync([caseToken]);
        _dispatcherMock.Setup(d => d.PublishCaseTokenAsync(It.IsAny<CaseToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _engine.TriggerUserEventAsync(caseId, eventId, new Dictionary<string, object> { { "key", "value" } }, CancellationToken.None);

        // Assert
        _dispatcherMock.Verify(d => d.PublishCaseTokenAsync(It.Is<CaseToken>(t => t.CurrentPlanItemId == eventId), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task PublishCaseFileUpdateAsync_KafkaIntegration_Successfully()
    {
        // Arrange
        var caseId = "D2DD968A-6748-4C17-83A6-8EAE354F5C77";
        var caseFileItemId = "amount";
        var newValue = 300;
        var caseModel = new CaseModel(
            caseId,
            "Test Case",
            [
                new PlanItem("event1", "eventListener", "caseFileItemUpdate", null, null)
            ],
            [],
            [
                new CaseFileItem(caseFileItemId, "Amount", 200)
            ]
        );
        var caseToken = new CaseToken(Guid.NewGuid(), Guid.Parse(caseId), "event1", "eventListener", new Dictionary<string, object> { { caseFileItemId, 200 } }, DateTime.UtcNow);

        _storeMock.Setup(s => s.GetCmmnModelAsync(caseId)).ReturnsAsync("<cmmn:case id='D2DD968A-6748-4C17-83A6-8EAE354F5C77'>...</cmmn:case>");
        _cmmnParserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(caseModel);
        _storeMock.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync([caseToken]);
        _storeMock.Setup(s => s.UpdateCaseModelAsync(It.IsAny<CaseModel>())).Returns(Task.CompletedTask);
        _dispatcherMock.Setup(d => d.PublishCaseFileUpdateAsync(It.IsAny<CaseFileUpdateEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _dispatcherMock.Setup(d => d.PublishCaseTokenAsync(It.IsAny<CaseToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _engine.UpdateCaseFileItemAsync(caseId, caseFileItemId, newValue, CancellationToken.None);

        // Assert
        _dispatcherMock.Verify(d => d.PublishCaseFileUpdateAsync(It.Is<CaseFileUpdateEvent>(e => e.CaseId == caseId && e.CaseFileItemId == caseFileItemId && e.NewValue.Equals(newValue)), It.IsAny<CancellationToken>()), Times.Once());
        //_dispatcherMock.Verify(d => d.PublishCaseTokenAsync(It.Is<CaseToken>(t => t.CaseFile[caseFileItemId].Equals(newValue)), It.IsAny<CancellationToken>()), Times.Once());
    }


    [Fact]
    public async Task ProcessTaskAsync_BusinessRuleTask_EvaluatesDmnCorrectly()
    {
        var logger = new LoggerFactory().CreateLogger<DistributedProcessEngine>();
        var registry = new ServiceTaskRegistry();
        var dispatcher = new Mock<IMessageDispatcher>();
        var store = new Mock<IProcessInstanceStore>();
        var dmnParser = new Mock<IDmnParser>();
        var cmmnParser = new Mock<ICmmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        var aiService = new Mock<IAiDecisionService>();

        // Ensure worker list is non-null to avoid null propagation
        store.Setup(s => s.GetActiveWorkersAsync()).ReturnsAsync(new List<WorkerNode>());

        var decision = new DmnDecision("decision1", "Test Decision",
            new List<DmnInput> { new((string)"input1", (string)"Amount", (string)"double") },
            new List<DmnOutput> { new((string)"output1", (string)"Result", (string)"string") },
            new List<DmnRule> { new((string) "rule1", (IReadOnlyDictionary<string, string>) new Dictionary<string, string> { { "input1", "> 100" } },
                (IReadOnlyDictionary<string, object>) new Dictionary<string, object> { { "output1", "Approved" } }) });

        dmnParser.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(decision);
        dmnEngine.Setup(e => e.EvaluateDecisionAsync(decision, It.IsAny<Dictionary<string, object>>(), CancellationToken.None))
                 .ReturnsAsync(new Dictionary<string, object> { { "output1", "Approved" } });
        store.Setup(s => s.GetDmnModelAsync("decision1", CancellationToken.None)).ReturnsAsync("<dmn:decision id='decision1'>...</dmn:decision>");

        var engine = new DistributedProcessEngine(logger, registry, dispatcher.Object, store.Object, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
        var model = new BpmnModel("process1", "process1",
            new List<BpmnEvent>(),
            new List<BpmnTask> {
                    new("task1", "businessRuleTask",  null,new Dictionary<string, string> { { "camunda:decisionRef", "decision1" }, { "camunda:resultVariable", "decisionResult" } }) },
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow> { new("flow1", "task1", "end1") },
            new List<BpmnSubprocess>());

        var token = new ExecutionToken(Guid.NewGuid(), Guid.NewGuid(), "task1", "businessRuleTask",
            new Dictionary<string, object> { { "input1", 200.0 } }, DateTime.UtcNow);
        var trace = new List<string>();

        var method = engine.GetType().GetMethod("ProcessTaskAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task?)method.Invoke(engine, new object[] { model.Tasks[0], token, model, trace, CancellationToken.None });
        Assert.NotNull(task);
        await task;

        Assert.Contains($"BusinessRuleTaskCompleted: task1 result stored in decisionResult", trace);
        Assert.True(token.Variables.ContainsKey("decisionResult"));
        Assert.Equal("Approved", ((Dictionary<string, object>)token.Variables["decisionResult"])["output1"]);
    }

    [Fact]
    public async Task ProcessTaskAsync_BusinessRuleTask_UsesVertexDecisionAndIoMappings()
    {
        var logger = new LoggerFactory().CreateLogger<DistributedProcessEngine>();
        var dispatcher = new Mock<IMessageDispatcher>();
        var store = new Mock<IProcessInstanceStore>();
        var dmnParser = new Mock<IDmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        store.Setup(s => s.GetActiveWorkersAsync()).ReturnsAsync(new List<WorkerNode>());
        store.Setup(s => s.GetDmnModelAsync("risk-assessment", CancellationToken.None)).ReturnsAsync("<definitions />");

        var decision = new DmnDecision("risk-assessment", "Risk", [], [], [], "UNIQUE");
        dmnParser.Setup(parser => parser.ParseAsync("<definitions />", CancellationToken.None)).ReturnsAsync(decision);
        dmnEngine.Setup(engine => engine.EvaluateDecisionAsync(
                decision,
                It.Is<Dictionary<string, object>>(input => input.Count == 1 && Equals(input["amount"], 250)),
                CancellationToken.None))
            .ReturnsAsync(new Dictionary<string, object> { ["risk"] = "low" });

        var engine = new DistributedProcessEngine(logger, new ServiceTaskRegistry(), dispatcher.Object, store.Object,
            dmnEngine.Object, dmnParser.Object, new Mock<ICmmnParser>().Object, new Mock<IBpmnParser>().Object,
            new Mock<IAiDecisionService>().Object, TracerProvider.Default);
        var task = new BpmnTask("decisionTask", "businessRuleTask", null, new Dictionary<string, string>
        {
            ["vertex:decision.decisionRef"] = "risk-assessment",
            ["vertex:ioMapping.input.amount"] = "${orderAmount}",
            ["vertex:ioMapping.output.risk"] = "riskLevel"
        });
        var model = new BpmnModel("p1", "p1", [], [task], [], [new BpmnSequenceFlow("f1", "decisionTask", "end")], []);
        var token = new ExecutionToken(Guid.NewGuid(), Guid.NewGuid(), "decisionTask", "businessRuleTask",
            new Dictionary<string, object> { ["orderAmount"] = 250 }, DateTime.UtcNow);
        var trace = new List<string>();

        var method = engine.GetType().GetMethod("ProcessTaskAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var execution = (Task?)method!.Invoke(engine, [task, token, model, trace, CancellationToken.None]);
        await execution!;

        dmnEngine.VerifyAll();
        Assert.Equal("low", token.Variables["riskLevel"]);
        Assert.Equal("low", ((Dictionary<string, object>)token.Variables["decisionResult"])["risk"]);
    }

    [Fact]
    public async Task ExecuteAsync_SimpleProcess_DistributesToken()
    {
        var simpleXml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
                                  <process id='p1' name='Simple'>
                                    <startEvent id='start1'/>
                                    <userTask id='task1' name='User Task'/>
                                    <endEvent id='end1'/>
                                    <sequenceFlow id='flow1' sourceRef='start1' targetRef='task1'/>
                                    <sequenceFlow id='flow2' sourceRef='task1' targetRef='end1'/>
                                  </process>
                                </definitions>";
        var logger1 = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger1.Object, TracerProvider.Default);
        var model = await parser.ParseAsync(simpleXml);

        var logger = new LoggerFactory().CreateLogger<DistributedProcessEngine>();
        var registry = new ServiceTaskRegistry();
        var dispatcher = new Mock<IMessageDispatcher>();
        var store = new InMemoryProcessInstanceStore();
        var dmnParser = new Mock<IDmnParser>();
        var cmmnParser = new Mock<ICmmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        var aiService = new Mock<IAiDecisionService>();
        //store.Setup(s => s.SaveWorkerAsync(It.IsAny<WorkerNode>())).Returns(Task.CompletedTask);
        //store.Setup(s => s.GetActiveWorkersAsync()).ReturnsAsync(new List<WorkerNode>()); // no workers needed
        //store.Setup(s => s.SaveTokenAsync(It.IsAny<ExecutionToken>())).Returns(Task.CompletedTask);
        dispatcher.Setup(d => d.PublishTokenAsync(It.IsAny<ExecutionToken>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);


        var engine = new DistributedProcessEngine(logger, registry, dispatcher.Object, store, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
        var trace = await engine.ExecuteAsync(model);

        Assert.Contains(trace, l => l.StartsWith("DistributedExecution: Starting process"));
        Assert.Contains("Start->Token:task1", trace);
        //store.Verify(s => s.SaveTokenAsync(It.IsAny<ExecutionToken>()), Times.Once);
        dispatcher.Verify(d => d.PublishTokenAsync(It.IsAny<ExecutionToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ExternalTaskFile_DistributesToken()
    {
        var path = Path.Combine("TestData", "ExternalTask1.bpmn");
        var xml = await File.ReadAllTextAsync(path);
        var logger1 = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger1.Object, TracerProvider.Default);
        var model = await parser.ParseAsync(xml);

        var logger = new LoggerFactory().CreateLogger<DistributedProcessEngine>();
        var registry = new ServiceTaskRegistry();
        var dispatcher = new Mock<IMessageDispatcher>();

        var store = new InMemoryProcessInstanceStore();
        var dmnParser = new Mock<IDmnParser>();
        var cmmnParser = new Mock<ICmmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        var aiService = new Mock<IAiDecisionService>();

        //store.Setup(s => s.SaveWorkerAsync(It.IsAny<WorkerNode>())).Returns(Task.CompletedTask);
        //store.Setup(s => s.GetActiveWorkersAsync()).ReturnsAsync(new List<WorkerNode>()); // no workers
        //store.Setup(s => s.SaveTokenAsync(It.IsAny<ExecutionToken>())).Returns(Task.CompletedTask);
        dispatcher.Setup(d => d.PublishTokenAsync(It.IsAny<ExecutionToken>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var engine = new DistributedProcessEngine(logger, registry, dispatcher.Object, store, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
        var trace = await engine.ExecuteAsync(model);

        Assert.Contains(trace, l => l.StartsWith("DistributedExecution: Starting process"));
        Assert.Contains("Start->Token:task1", trace);
        //store.Verify(s => s.SaveTokenAsync(It.IsAny<ExecutionToken>()), Times.Once);
        dispatcher.Verify(d => d.PublishTokenAsync(It.IsAny<ExecutionToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public async Task ExecuteAsync_With_the_EngineBuilder()
    {

        // 1. Definiere ein einfaches BPMN 2.0-Prozessmodell als XML-String
        var path = Path.Combine("TestData", "hello-world.bpmn");
        var bpmnProcess = await File.ReadAllTextAsync(path);

        // 2. Baue eine In-Memory-Engine für einen schnellen Test
        var engine = await new EngineBuilder()
            .UseInMemoryStorage()
            .UseDistributedExecution()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IServiceTaskRegistry, ServiceTaskRegistry>();
                services.AddSingleton<IMessageDispatcher, InMemoryMessageDispatcher>();
                services.AddSingleton<IProcessInstanceStore, InMemoryProcessInstanceStore>();
                services.AddSingleton<IAiDecisionService, FakeAiDecisionService>();
                services.AddSingleton(TracerProvider.Default);
                // Add any additional services or overrides here if needed
            })
            .BuildAsync();

        // 3. Deploye den Prozess in die Engine
          await engine.RegisterProcessAsync("Process_HelloWorld", bpmnProcess);

        _testOutputHelper.WriteLine($"Prozess erfolgreich deployed.");

        // 4. Starte eine neue Instanz des Prozesses
        var processInstance = await engine.StartInstanceAsync("Process_HelloWorld", null);

        _testOutputHelper.WriteLine($"Prozessinstanz mit der ID '{processInstance}' wurde gestartet!");


    }
    [Fact]
    public async Task NoneEndEvent_IsCompletedWithoutFollowingSequenceFlow()
    {
        var store =
            new InMemoryProcessInstanceStore();

        var engine =
            CreateTestEngine(store);

        var token = CreateToken();

        var evt = new BpmnEvent(
            Id: "end",
            Type: "endEvent",
            Definitions: Array.Empty<EventDefinition>());

        var model = CreateModel();
        var trace = new List<string>();

        await InvokeProcessEventAsync(
            engine,
            evt,
            token,
            model,
            trace);

        token.State.ShouldBe(
            ExecutionToken.CompletedState);

        var persistedToken =
            await store.GetTokenAsync(token.Id);

        persistedToken.State.ShouldBe(
            ExecutionToken.CompletedState);

        trace.ShouldContain(
            entry => entry.Contains("EndEvent"));
    }

    [Fact]
    public async Task NoneStartEvent_UsesNormalSequenceFlowContinuation()
    {
        SetExecutionComponent(_engine);

        var startEvent = new BpmnEvent(
            Id: "start",
            Type: "startEvent",
            Definitions: Array.Empty<EventDefinition>());

        var model = new BpmnModel(
            ProcessId: "process",
            Name: "process",
            Events: new[] { startEvent },
            Gateways: Array.Empty<BpmnGateway>(),
            Subprocesses: Array.Empty<BpmnSubprocess>(),
            SequenceFlows: Array.Empty<BpmnSequenceFlow>(),
            Tasks: Array.Empty<BpmnTask>());

        var trace = new List<string>();

        await InvokeProcessEventAsync(
            _engine,
            startEvent,
            token: null!,
            model,
            trace);

        trace.ShouldHaveSingleItem().ShouldBe("StartEvent: start");
    }

    [Fact]
    public async Task EventWithMoreThanOneDefinition_IsRejected()
    {

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

        var act = () => InvokeProcessEventAsync(
            _engine,
            evt,
            token: null!,
            model: null!,
            trace: new List<string>());

        await act.ShouldThrowAsync<DistributedTokenException>("*contains 2 event definitions*");
    }

    [Fact]
    public async Task TimerEvent_IsRejectedInsteadOfUsingFakeDelay()
    {
        var evt = new BpmnEvent(
            Id: "timer-catch",
            Type: "intermediateCatchEvent",
            Definitions: new EventDefinition[]
            {
                new TimerEventDefinition(
                    TimeDate: null,
                    TimeDuration: "PT10S",
                    TimeCycle: null)
            });

        var act = () => InvokeProcessEventAsync(
            _engine,
            evt,
            token: null!,
            model: null!,
            trace: new List<string>());

        await act.ShouldThrowAsync<DistributedTokenException>("*Persistent timer waiting is not implemented*");
    }

    [Fact]
    public async Task MessageEvent_PersistsWaitingToken_AndRegistersSubscription()
    {
        var evt = new BpmnEvent(
            Id: "message-catch",
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
        var trace = new List<string>();

        await InvokeProcessEventAsync(_engine, evt, token, CreateModel(), trace);

        token.State.ShouldBe(ExecutionToken.WaitingState);
        trace.ShouldContain("MessageEventWaiting: message-catch for message order-approved");
        _storeMock.Verify(store => store.SaveTokenAsync(token), Times.Once);
        _dispatcherMock.Verify(dispatcher => dispatcher.SubscribeToMessageAsync(
            "order-approved", It.IsAny<Func<Message, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignalEvent_IsRejectedExplicitly()
    {

        var evt = new BpmnEvent(
            Id: "signal-catch",
            Type: "intermediateCatchEvent",
            Definitions: new EventDefinition[]
            {
                new SignalEventDefinition(
                    SignalRef: "order-approved")
            });

        var act = () => InvokeProcessEventAsync(
            _engine,
            evt,
            token: null!,
            model: null!,
            trace: new List<string>());

        await act.ShouldThrowAsync<DistributedTokenException>("*Signal subscription handling is not implemented*");
    }

    [Fact]
    public async Task BoundaryEventWithoutDefinition_IsRejected()
    {
        var evt = new BpmnEvent(
            Id: "boundary",
            Type: "boundaryEvent",
            Definitions: Array.Empty<EventDefinition>());

        var act = () => InvokeProcessEventAsync(
            _engine,
            evt,
            token: null!,
            model: null!,
            trace: new List<string>());

        await act.ShouldThrowAsync<DistributedTokenException>("*has no event definition*");
    }

    [Fact]
    public async Task UnknownEventType_IsRejectedWithoutContinuation()
    {
        var trace = new List<string>();

        var evt = new BpmnEvent(
            Id: "unknown",
            Type: "unknownEvent",
            Definitions: Array.Empty<EventDefinition>());

        var act = () => InvokeProcessEventAsync(
            _engine,
            evt,
            token: null!,
            model: null!,
            trace);

        await act.ShouldThrowAsync<DistributedTokenException>("*Unsupported BPMN event type*");

        trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task EventDefinitionOnStartEvent_IsNotTreatedAsNoneStartEvent()
    {
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

        var act = () => InvokeProcessEventAsync(
            _engine,
            evt,
            token: null!,
            model: null!,
            trace);

        await act.ShouldThrowAsync<DistributedTokenException>();

        trace.ShouldNotContain("StartEvent: timer-start");
    }

    private static void SetExecutionComponent(
        DistributedProcessEngine engine)
    {
        var field = typeof(DistributedProcessEngine)
            .GetField(
                "_executionComponent",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        field.ShouldNotBeNull();

        field!.SetValue(
            engine,
            new BpmnExecutionComponent());
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
    private static BpmnModel CreateModel()
    {
        return new BpmnModel(
            ProcessId: "process",
            Name: "process");
    }

    private static DistributedProcessEngine CreateTestEngine(
        IProcessInstanceStore store)
    {
        var engine =
            (DistributedProcessEngine)
            RuntimeHelpers.GetUninitializedObject(
                typeof(DistributedProcessEngine));

        SetPrivateField(
            engine,
            "_store",
            store);

        SetPrivateField(
            engine,
            "_logger",
            NullLogger<DistributedProcessEngine>.Instance);

        return engine;
    }

    private static void SetPrivateField(
        DistributedProcessEngine engine,
        string fieldName,
        object value)
    {
        var field = typeof(DistributedProcessEngine)
            .GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        field.ShouldNotBeNull();

        field!.SetValue(
            engine,
            value);
    }

    private static ExecutionToken CreateToken()
    {
        return new ExecutionToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "node",
            "event");
    }
}

