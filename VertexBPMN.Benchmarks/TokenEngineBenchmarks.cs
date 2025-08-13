using System;
using System.Collections.Generic;
using System.Diagnostics;
using VertexBPMN.Core.Bpmn;
using Xunit;

namespace VertexBPMN.Benchmarks;

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
            new List<BpmnSubprocess>(),
            new List<BpmnSequenceFlow> { new("flow1", "start1", "end1") }
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
}
