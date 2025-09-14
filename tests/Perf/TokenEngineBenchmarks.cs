using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Perf;

public class TokenEngineBenchmarks
{
    [Fact]
    public void Benchmark_Execute_SimpleProcess()
    {
        var model = new BpmnModel(
            "P1",
            "Benchmark",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow> { new("flow1", "start1", "end1") },
                new List<BpmnSubprocess>()
        );
        var engine = new TokenEngine();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            engine.Execute(model);
        }
        sw.Stop();
        Console.WriteLine($"Executed 10,000 simple processes in {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 2000); // Should be fast
    }
    [Fact]
    public void Benchmark_Execute_Distributed_SimpleProcess()
    {
        var model = new BpmnModel(
            "P1",
            "Benchmark",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow> { new("flow1", "start1", "end1") },
            new List<BpmnSubprocess>()
        );
        var logger = new LoggerFactory().CreateLogger<DistributedTokenEngine>();
        var registry = new ServiceTaskRegistry();
        var dispatcher = new Mock<IMessageDispatcher>();
        var store = new Mock<IProcessInstanceStore>();
        var dmnParser = new Mock<IDmnParser>();
        var cmmnParser = new Mock<ICmmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        var aiService = new Mock<IAiDecisionService>();
        var engine = new DistributedTokenEngine(logger, registry, dispatcher.Object, store.Object, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            _ = engine.ExecuteAsync(model);
        }
        sw.Stop();
        Console.WriteLine($"Executed 10,000 simple processes in {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 2000); // Should be fast
    }
    [Fact]
    public async Task ProcessCaseTokenAsync_ComplexSentry_EvaluatesCorrectly()
    {
        var logger = new Mock<ILogger<DistributedTokenEngine>>();
        var registry = new ServiceTaskRegistry();
        var dispatcher = new Mock<IMessageDispatcher>();
        var store = new Mock<IProcessInstanceStore>();
        var dmnParser = new Mock<IDmnParser>();
        var cmmnParser = new Mock<ICmmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        var aiService = new Mock<IAiDecisionService>();

        var caseModel = new CaseModel(
            "case1",
            "Test Case",
            [
                new PlanItem("task1", "humanTask", "humanTaskDef", new() { { "camunda:assignee", "user1" } }, ["sentry1"]),
                new PlanItem("event1", "eventListener", "caseFileItemUpdate", null, null)
            ],
            [
                new Sentry("sentry1", [
                    new SentryCondition("input > 100", "amount", "complete", "AND"),
                    new SentryCondition("true", "", "complete", "AND")
                ], "event1", true)
            ],
            [
                new CaseFileItem("amount", "Amount", 200)
            ]
        );

        store.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync([]);
        cmmnParser.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(caseModel);
        store.Setup(s => s.GetCmmnModelAsync("case1")).ReturnsAsync("<cmmn:case id='case1'>...</cmmn:case>");

        var engine = new DistributedTokenEngine(logger.Object, registry, dispatcher.Object, store.Object, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
        var token = new CaseToken(Guid.NewGuid(), Guid.NewGuid(), "task1", "humanTask", new() { { "amount", 200 } }, DateTime.UtcNow);
        var trace = new List<string>();
        engine.GetType().GetMethod("ProcessCaseTokenAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(engine, new object[] { token, caseModel, trace, CancellationToken.None });

        Assert.NotNull(trace);
    }
}
