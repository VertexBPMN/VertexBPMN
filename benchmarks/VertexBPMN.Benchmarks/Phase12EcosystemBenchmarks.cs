using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using VertexBPMN.Parsing;
using VertexBPMN.Parsing.Ecosystem;
using VertexBPMN.Test.Parsing.Ecosystem;

namespace VertexBPMN.Benchmarks;

/// <summary>
/// Phase 12: Benchmarks for ecosystem features.
/// Measures performance impact of streaming parsing and vendor handlers.
/// </summary>
[MemoryDiagnoser]
public class Phase12EcosystemBenchmarks
{
    private BpmnParser _standardParser = null!;
    private BpmnStreamingParser _streamingParser = null!;
    private string _largeModel = null!;

    [GlobalSetup]
    public void Setup()
    {
        _standardParser = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Normalized,
            EnableStreamingParse = false
        });

        _streamingParser = new BpmnStreamingParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Normalized,
            EnableStreamingParse = true,
            StreamingThreshold = 1024 // Low threshold for benchmarking
        });

        _largeModel = GenerateLargeTestModel(5000);
    }

    [Benchmark]
    public async Task<Domain.Model.Bpmn.BpmnModel> StandardParse_LargeModel()
    {
        return await _standardParser.ParseAsync(_largeModel);
    }

    [Benchmark]
    public async Task<Domain.Model.Bpmn.BpmnModel> StreamingParse_LargeModel()
    {
        return await _streamingParser.ParseAsync(_largeModel);
    }

    [Benchmark]
    public async Task<Domain.Model.Bpmn.BpmnModel> VendorHandlerParse()
    {
        var parser = new BpmnParser(new BpmnParserOptions
        {
            VendorExtensionHandlers = new[] { new TestCustomVendorHandler() }
        });

        var xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:custom="http://test.vendor/extensions">
  <process id="test">
    <userTask id="task1">
      <extensionElements>
        <custom:specialProcessor type="fast"/>
      </extensionElements>
    </userTask>
  </process>
</definitions>
""";

        return await parser.ParseAsync(xml);
    }

    private static string GenerateLargeTestModel(int elementCount)
    {
        var tasks = new string[elementCount];
        var flows = new string[elementCount - 1];
        
        for (int i = 1; i <= elementCount; i++)
        {
            tasks[i - 1] = $"<userTask id=\"task{i}\" name=\"Task {i}\"/>";
            if (i > 1)
            {
                flows[i - 2] = $"<sequenceFlow id=\"f{i-1}\" sourceRef=\"task{i-1}\" targetRef=\"task{i}\"/>";
            }
        }
        
        return $"""
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="largeTestProcess">
    <startEvent id="start"/>
    {string.Join("\n    ", tasks)}
    <endEvent id="end"/>
    
    <sequenceFlow id="f0" sourceRef="start" targetRef="task1"/>
    {string.Join("\n    ", flows)}
    <sequenceFlow id="f{elementCount}" sourceRef="task{elementCount}" targetRef="end"/>
  </process>
</definitions>
""";
    }
}