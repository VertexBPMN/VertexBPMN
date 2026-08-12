using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Engine.Execution;

namespace PerformanceRunner.Perf;

[MemoryDiagnoser]
public class DistributedTokenEngineBenchmarks
{
    private readonly DistributedProcessEngine _engine;
    private readonly CaseModel _caseModel;
    private readonly CaseToken _caseToken;

    public DistributedTokenEngineBenchmarks()
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


        _caseModel = new CaseModel(
            "case1",
            "Test Case",
            [
                new PlanItem("task1", "humanTask", "humanTaskDef", new() { { "camunda:assignee", "user1" } }, ["sentry1"]),
                    new PlanItem("event1", "eventListener", "caseFileItemUpdate", null, null)
            ],
            [
                new Sentry("sentry1", [
                        new SentryCondition("input > 100", "amount", "complete", "AND")
                    ], "event1", true)
            ],
            [
                new CaseFileItem("amount", "Amount", 200)
            ]
        );

        _caseToken = new CaseToken(Guid.NewGuid(), Guid.NewGuid(), "task1", "humanTask", new() { { "amount", 200 } }, DateTime.UtcNow);

        store.Setup(s => s.GetCmmnModelAsync("case1")).ReturnsAsync("<cmmn:case id='case1'>...</cmmn:case>");
        cmmnParser.Setup(p => p.ParseAsync(It.IsAny<string>(),CancellationToken.None )).ReturnsAsync(_caseModel);
        store.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync([_caseToken]);

        _engine = new DistributedProcessEngine(logger, registry, dispatcher.Object, store.Object, dmnEngine.Object, dmnParser.Object, cmmnParser.Object, bpmnParser.Object, aiService.Object, TracerProvider.Default);

    }

    [Benchmark]
    public async Task BenchmarkSentryEvaluation()
    {
        var trace = new List<string>();
         _engine.GetType().GetMethod("ProcessCaseTokenAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_engine, new object[] { _caseToken, _caseModel, trace, CancellationToken.None });
    }

    [Benchmark]
    public async Task BenchmarkCaseFileUpdate()
    {
        await _engine.UpdateCaseFileItemAsync("case1", "amount", 300, CancellationToken.None);
    }
}