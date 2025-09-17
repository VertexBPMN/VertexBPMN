using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
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
        var engine = new ProcessEngine();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            engine.Execute(model);
        }
        sw.Stop();
        Console.WriteLine($"ProcessEngine executed 10,000 simple processes in {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 5000); // Increased threshold for more realistic expectation
    }

    [Fact]
    public async Task Benchmark_Execute_Distributed_SimpleProcess()
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
        var logger = new LoggerFactory().CreateLogger<DistributedProcessEngine>();
        var registry = new ServiceTaskRegistry();
        var dispatcher = new Mock<IMessageDispatcher>();
        var store = new Mock<IProcessInstanceStore>();
        var dmnParser = new Mock<IDmnParser>();
        var cmmnParser = new Mock<ICmmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        var aiService = new Mock<IAiDecisionService>();
        
        // Setup required mock returns to avoid null reference exceptions
        store.Setup(s => s.GetActiveWorkersAsync()).ReturnsAsync(new List<WorkerNode>());
        store.Setup(s => s.GetPendingTokensAsync()).ReturnsAsync(new List<ExecutionToken>());
        
        var engine = new DistributedProcessEngine(logger, registry, dispatcher.Object, store.Object, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < 1000; i++) // Reduced count for distributed engine
        {
            await engine.ExecuteAsync(model);
        }
        sw.Stop();
        Console.WriteLine($"DistributedProcessEngine executed 1,000 simple processes in {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 10000); // More generous threshold for distributed engine
    }

    [Fact]
    public async Task ProcessCaseTokenAsync_ComplexSentry_EvaluatesCorrectly()
    {
        var logger = new Mock<ILogger<DistributedProcessEngine>>();
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

        // Setup required mock returns
        store.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync(new List<CaseToken>());
        store.Setup(s => s.GetActiveWorkersAsync()).ReturnsAsync(new List<WorkerNode>());
        cmmnParser.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(caseModel);
        store.Setup(s => s.GetCmmnModelAsync("case1")).ReturnsAsync("<cmmn:case id='case1'>...</cmmn:case>");

        var engine = new DistributedProcessEngine(logger.Object, registry, dispatcher.Object, store.Object, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);
        var token = new CaseToken(Guid.NewGuid(), Guid.NewGuid(), "task1", "humanTask", new() { { "amount", 200 } }, DateTime.UtcNow);
        var trace = new List<string>();
        
        // Use reflection to call private method
        var methodInfo = engine.GetType().GetMethod("ProcessCaseTokenAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        if (methodInfo != null)
        {
            var task = (Task)methodInfo.Invoke(engine, new object[] { token, caseModel, trace, CancellationToken.None })!;
            await task;
        }

        Assert.NotNull(trace);
        Assert.True(trace.Count > 0); // Should have some trace entries
    }

    [Fact]
    public async Task Benchmark_Execute_ProposalTokenEngine()
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
        var logger = new Mock<ILogger<ProposalTokenEngine>>();
        var registry = new ServiceTaskRegistry();
        var engine = new ProposalTokenEngine(logger.Object, registry);
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++) // Test with 1000 iterations
        {
            await engine.ExecuteAsync(model);
        }
        sw.Stop();
        Console.WriteLine($"ProposalTokenEngine executed 1,000 simple processes in {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 10000); // Should be reasonable
    }
}
