using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence.InMemory;
using VertexBPMN.Infrastructure.Persistence.Repositories;
using Xunit;

namespace PerformanceRunner.Perf;

public class AdvancedTokenEngineBenchmarks
{
    [Fact]
    public void Benchmark_Execute_ComplexProcess()
    {
        var model = new BpmnModel(
             "P2",
            "ComplexBenchmark",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnGateway> { new("gw1", "parallelGateway") },
            new List<BpmnSubprocess> { new("sub1", true) },
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "gw1"),
                new("flow2", "gw1", "t1"),
                new("flow3", "gw1", "sub1"),
                new("flow4", "t1", "brt1"),
                new("flow5", "sub1", "brt1"),
                new("flow6", "brt1", "end1")
            },
            new List<BpmnTask> { new("t1", "userTask"), new("brt1", "businessRuleTask") }
        );
        var engine = new ProcessEngine();
        var logger = new LoggerFactory().CreateLogger<DecisionService>();
        var decisionService = new DecisionService(logger, new InMemoryDecisionRepository());
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 5000; i++)
        {
          var result =  engine.Execute(model, decisionService);
            Assert.NotNull(result);
            Assert.Contains("StartEvent: start1", result);
        }
        sw.Stop();
        Console.WriteLine($"Executed 5,000 complex processes in {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 3000); // Should be performant
    }
}
