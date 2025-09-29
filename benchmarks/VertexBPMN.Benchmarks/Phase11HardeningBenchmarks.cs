using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Security;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Security;

namespace VertexBPMN.Benchmarks;

/// <summary>
/// Phase 11: Benchmarks for hardening features.
/// Measures performance impact of security and resilience features.
/// </summary>
[MemoryDiagnoser]
public class Phase11HardeningBenchmarks
{
    private BpmnParser _baselineParser = null!;
    private BpmnParser _hardenedParser = null!;
    private string _complexModel = null!;
    private BpmnMemoryProfiler _profiler = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baselineParser = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Normalized,
            EnableAdvancedValidation = false,
            InternIds = false,
            OptimizeLargeModels = false
        });

        _hardenedParser = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true,
            ThrowOnFatalValidation = false,
            InternIds = true,
            OptimizeLargeModels = true,
            UseLazyRawCloning = true,
            UseSharedStringPool = true
        });

        _complexModel = GenerateComplexTestModel();
        _profiler = new BpmnMemoryProfiler();
    }

    [Benchmark]
    public async Task<Domain.Model.Bpmn.BpmnModel> Baseline_Parse()
    {
        return await _baselineParser.ParseAsync(_complexModel);
    }

    [Benchmark]
    public async Task<Domain.Model.Bpmn.BpmnModel> Hardened_Parse()
    {
        return await _hardenedParser.ParseAsync(_complexModel);
    }

    [Benchmark]
    public async Task<MemoryProfileSnapshot> Memory_Profiling_Overhead()
    {
        return await _profiler.ProfileParseOperationAsync(_complexModel, new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Normalized,
            InternIds = true
        });
    }

    private static string GenerateComplexTestModel()
    {
        return """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" 
             xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <process id="complexBenchmarkProcess">
    <startEvent id="start"/>
    <parallelGateway id="fork1"/>
    <userTask id="task1" name="Review Document">
      <extensionElements>
        <camunda:assignee value="reviewer"/>
        <camunda:formField id="field1" type="string"/>
      </extensionElements>
    </userTask>
    <serviceTask id="service1" name="Process Data"/>
    <exclusiveGateway id="decision1"/>
    <userTask id="approve" name="Approve"/>
    <userTask id="reject" name="Reject"/>
    <parallelGateway id="join1"/>
    <endEvent id="end"/>
    
    <sequenceFlow id="f1" sourceRef="start" targetRef="fork1"/>
    <sequenceFlow id="f2" sourceRef="fork1" targetRef="task1"/>
    <sequenceFlow id="f3" sourceRef="fork1" targetRef="service1"/>
    <sequenceFlow id="f4" sourceRef="task1" targetRef="decision1"/>
    <sequenceFlow id="f5" sourceRef="decision1" targetRef="approve">
      <conditionExpression>#{approved}</conditionExpression>
    </sequenceFlow>
    <sequenceFlow id="f6" sourceRef="decision1" targetRef="reject">
      <conditionExpression>#{!approved}</conditionExpression>
    </sequenceFlow>
    <sequenceFlow id="f7" sourceRef="service1" targetRef="join1"/>
    <sequenceFlow id="f8" sourceRef="approve" targetRef="join1"/>
    <sequenceFlow id="f9" sourceRef="reject" targetRef="join1"/>
    <sequenceFlow id="f10" sourceRef="join1" targetRef="end"/>
  </process>
</definitions>
""";
    }
}