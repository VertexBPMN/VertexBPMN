using BenchmarkDotNet.Running;

namespace VertexBPMN.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly); switcher.Run(args);
        //BenchmarkRunner.Run<BpmnParserPerformanceBenchmarks>();
    }
}