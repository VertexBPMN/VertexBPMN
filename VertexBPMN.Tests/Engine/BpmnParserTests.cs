using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using System.Reflection;
using System.Text.Json;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Dmn;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Exceptions;
using VertexBPMN.Core.Messaging;
using VertexBPMN.Core.Services;

// added for reading BPMN file

namespace VertexBPMN.Tests.Engine
{
    public class BpmnParserTests
    {
        [Fact]
        public async Task ParseAsync_FlowableTaskListener_ParsesCorrectly()
        {
            var logger = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:flowable='http://flowable.org/bpmn'>
                  <process id='process1'>
                    <userTask id='task1'>
                      <extensionElements>
                        <flowable:taskListener event='create' class='com.example.MyTaskListener'/>
                      </extensionElements>
                    </userTask>
                  </process>
                </definitions>";
            var model = await parser.ParseAsync(xml);
            var task = model.Tasks.First();
            Assert.True(task.Attributes.ContainsKey("flowable:taskListeners"));
            var listeners = JsonSerializer.Deserialize<List<dynamic>>(task.Attributes["flowable:taskListeners"]);
            Assert.Equal("create", ((JsonElement)listeners[0]).GetProperty("Event").GetString());// (string)listeners[0].Event);
            Assert.Equal("com.example.MyTaskListener", ((JsonElement)listeners[0]).GetProperty("Class").GetString());
        }

        [Fact]
        public async Task ParseAsync_ValidBpmnXml_ReturnsCorrectModel()
        {
            var logger = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
                      <process id='process1' name='Test Process'>
                        <userTask id='task1' name='Test Task'/>
                      </process>
                    </definitions>";
            var model = await parser.ParseAsync(xml);
            Assert.NotNull(model);
            Assert.Equal("process1", model.Id);
            Assert.Single(model.Tasks);
            Assert.Equal("task1", model.Tasks[0].Id);
        }

        [Fact]
        public async Task ParseAsync_InvalidXml_ThrowsBpmnParseException()
        {
            var logger = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            await Assert.ThrowsAsync<BpmnParseException>(() => parser.ParseAsync("<invalid>"));
        }

        [Fact]
        public async Task ProcessTaskAsync_BusinessRuleTask_EvaluatesDmnCorrectly()
        {
            var logger = new LoggerFactory().CreateLogger<DistributedTokenEngine>();
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
                new List<DmnInput> { new("input1", "Amount", "double") },
                new List<DmnOutput> { new("output1", "Result", "string") },
                new List<DmnRule> { new("rule1", new Dictionary<string, string> { { "input1", "> 100" } },
                new Dictionary<string, object> { { "output1", "Approved" } }) });

            dmnParser.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(decision);
            dmnEngine.Setup(e => e.EvaluateDecisionAsync(decision, It.IsAny<Dictionary<string, object>>(), CancellationToken.None))
                     .ReturnsAsync(new Dictionary<string, object> { { "output1", "Approved" } });
            store.Setup(s => s.GetDmnModelAsync("decision1", CancellationToken.None)).ReturnsAsync("<dmn:decision id='decision1'>...</dmn:decision>");

            var engine = new DistributedTokenEngine(logger, registry, dispatcher.Object, store.Object, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
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
            Assert.Equal("Approved", ((Dictionary<string, object>)token.Variables["decisionResult"]) ["output1"]);
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

            var logger = new LoggerFactory().CreateLogger<DistributedTokenEngine>();
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


            var engine = new DistributedTokenEngine(logger, registry, dispatcher.Object, store, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
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

            var logger = new LoggerFactory().CreateLogger<DistributedTokenEngine>();
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

            var engine = new DistributedTokenEngine(logger, registry, dispatcher.Object, store, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
            var trace = await engine.ExecuteAsync(model);

            Assert.Contains(trace, l => l.StartsWith("DistributedExecution: Starting process"));
            Assert.Contains("Start->Token:task1", trace);
            //store.Verify(s => s.SaveTokenAsync(It.IsAny<ExecutionToken>()), Times.Once);
            dispatcher.Verify(d => d.PublishTokenAsync(It.IsAny<ExecutionToken>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}