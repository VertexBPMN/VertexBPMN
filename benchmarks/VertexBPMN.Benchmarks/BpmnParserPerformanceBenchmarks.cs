using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Parsing;

namespace VertexBPMN.Benchmarks;

[Config(typeof(Config))]
[MemoryDiagnoser]
public class BpmnParserPerformanceBenchmarks
{
    private BpmnParser _parserStrict = null!;
    private BpmnParser _parserNormalized = null!;
    private BpmnParser _parserWithInterning = null!;
    private BpmnParser _parserWithoutInterning = null!;
    private BpmnParser _optimizedParser = null!;
    
    // Test data for different model sizes
    private string _smallXml = null!;
    private string _mediumXml = null!;
    private string _largeXml = null!;
    
    private class Config : ManualConfig
    {
        public Config()
        {
            // Basic configuration - removed problematic ETW profiler
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _parserStrict = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            InternIds = true,
            BuildRuntimeProjection = false,
            EnableAdvancedValidation = false
        });

        _parserNormalized = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Normalized,
            InternIds = true,
            BuildRuntimeProjection = false,
            EnableAdvancedValidation = false
        });
        
        _parserWithInterning = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Normalized,
            InternIds = true,
            BuildRuntimeProjection = false,
            EnableAdvancedValidation = false
        });
        
        _parserWithoutInterning = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Normalized,
            InternIds = false,
            BuildRuntimeProjection = false,
            EnableAdvancedValidation = false
        });

        _optimizedParser = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            OptimizeLargeModels = true,  // Enable optimizations
            LargeModelThreshold = 100,
            SkipDocumentationForLargeModels = true,  // Enable this optimization
            SkipArtifactsForLargeModels = true,      // Enable this optimization
            SkipExtensionsForLargeModels = false,    // Keep extensions for compatibility
            UseLazyRawCloning = true,
            UseSharedStringPool = true,
            InternIds = true
        });

        // Small model: 5 elements
        _smallXml = GenerateSmallModel();
        
        // Medium model: ~50 elements
        _mediumXml = GenerateMediumModel();
        
        // Large model: ~500 elements  
        _largeXml = GenerateLargeModel();
    }

    [Benchmark(Baseline = true)]
    public async Task<BpmnModel> ParseSmall_Normalized()
    {
        return await _parserNormalized.ParseAsync(_smallXml);
    }

    [Benchmark]
    public async Task<BpmnModel> ParseSmall_Strict()
    {
        return await _parserStrict.ParseAsync(_smallXml);
    }

    [Benchmark]
    public async Task<BpmnModel> ParseMedium_Normalized()
    {
        return await _parserNormalized.ParseAsync(_mediumXml);
    }

    [Benchmark]
    public async Task<BpmnModel> ParseMedium_Strict()
    {
        return await _parserStrict.ParseAsync(_mediumXml);
    }

    [Benchmark]
    public async Task<BpmnModel> ParseLarge_Normalized()
    {
        return await _parserNormalized.ParseAsync(_largeXml);
    }

    [Benchmark]
    public async Task<BpmnModel> ParseLarge_Strict()
    {
        return await _parserStrict.ParseAsync(_largeXml);
    }
    
    [Benchmark]
    public async Task<BpmnModel> ParseMedium_WithInterning()
    {
        return await _parserWithInterning.ParseAsync(_mediumXml);
    }
    
    [Benchmark]
    public async Task<BpmnModel> ParseMedium_WithoutInterning()
    {
        return await _parserWithoutInterning.ParseAsync(_mediumXml);
    }

    [Benchmark]
    public async Task<BpmnModel> ParseLarge_Optimized()
    {
        return await _optimizedParser.ParseAsync(_largeXml);
    }

    [Benchmark]
    public async Task<BpmnModel> ParseLarge_WithDocumentationSkip()
    {
        var parser = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            OptimizeLargeModels = true,
            LargeModelThreshold = 100,
            SkipDocumentationForLargeModels = true,  // Enable this specific optimization
            InternIds = true
        });
        return await parser.ParseAsync(_largeXml);
    }

    [Benchmark]
    public async Task<BpmnModel> ParseLarge_WithArtifactSkip()
    {
        var parser = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            OptimizeLargeModels = true,
            LargeModelThreshold = 100,
            SkipArtifactsForLargeModels = true,  // Enable this specific optimization
            InternIds = true
        });
        return await parser.ParseAsync(_largeXml);
    }

    // Model generation methods
    private static string GenerateSmallModel()
    {
        return """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="smallProcess">
    <startEvent id="start"/>
    <userTask id="task1" name="Simple Task"/>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>
  </process>
</definitions>
""";
    }

    private static string GenerateMediumModel()
    {
        var xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" 
             xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <process id="mediumProcess">
    <startEvent id="start"/>
""";
        
        // Generate 10 parallel branches with 4 tasks each = ~40 tasks
        for (int branch = 0; branch < 10; branch++)
        {
            xml += $"""
    <parallelGateway id="fork{branch}"/>
    <userTask id="task{branch}_1" name="Task {branch}.1">
      <extensionElements>
        <camunda:assignee value="user{branch}"/>
      </extensionElements>
    </userTask>
    <serviceTask id="task{branch}_2" name="Service {branch}.2"/>
    <scriptTask id="task{branch}_3" scriptFormat="javascript">
      <script>console.log('Branch {branch}');</script>
    </scriptTask>
    <userTask id="task{branch}_4" name="Task {branch}.4"/>
    <parallelGateway id="join{branch}"/>
""";
        }
        
        xml += """
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="fork0"/>
""";
        
        // Generate sequence flows
        for (int branch = 0; branch < 10; branch++)
        {
            if (branch > 0)
                xml += $"""    <sequenceFlow id="f{branch * 10}" sourceRef="join{branch - 1}" targetRef="fork{branch}"/>""" + "\n";
                
            xml += $"""
    <sequenceFlow id="f{branch * 10 + 1}" sourceRef="fork{branch}" targetRef="task{branch}_1"/>
    <sequenceFlow id="f{branch * 10 + 2}" sourceRef="task{branch}_1" targetRef="task{branch}_2"/>
    <sequenceFlow id="f{branch * 10 + 3}" sourceRef="task{branch}_2" targetRef="task{branch}_3"/>
    <sequenceFlow id="f{branch * 10 + 4}" sourceRef="task{branch}_3" targetRef="task{branch}_4"/>
    <sequenceFlow id="f{branch * 10 + 5}" sourceRef="task{branch}_4" targetRef="join{branch}"/>
""";
        }
        
        xml += $"""    <sequenceFlow id="fend" sourceRef="join9" targetRef="end"/>""" + "\n";
        xml += """
  </process>
</definitions>
""";
        return xml;
    }

    private static string GenerateLargeModel()
    {
        var xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" 
             xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
             xmlns:zeebe="http://zeebe.io/schema/zeebe/1.0">
  <process id="largeProcess">
    <startEvent id="start"/>
""";
        
        // Generate 50 parallel branches with 8 tasks each = ~400 tasks
        for (int branch = 0; branch < 50; branch++)
        {
            xml += $"""
    <parallelGateway id="fork{branch}"/>
    <userTask id="task{branch}_1" name="User Task {branch}.1">
      <documentation>This is comprehensive documentation for task {branch}_1. 
      It contains detailed instructions, business context, and user guidance that would normally be preserved in strict mode.
      With large model optimizations, this documentation capture can be selectively skipped to save significant memory.</documentation>
      <extensionElements>
        <camunda:assignee value="user{branch}"/>
        <camunda:formField id="field{branch}" type="string"/>
        <camunda:properties>
          <camunda:property name="priority" value="high"/>
          <camunda:property name="category" value="branch{branch}"/>
        </camunda:properties>
        <camunda:taskListener event="create" class="org.example.TaskListener{branch}"/>
      </extensionElements>
    </userTask>
    <serviceTask id="task{branch}_2" name="Service Task {branch}.2">
      <documentation>Service task documentation for branch {branch} with detailed service configuration and error handling instructions.</documentation>
      <extensionElements>
        <zeebe:taskDefinition type="external"/>
        <zeebe:ioMapping>
          <zeebe:input source="=data" target="input{branch}"/>
          <zeebe:output source="=result" target="output{branch}"/>
        </zeebe:ioMapping>
        <zeebe:taskHeaders>
          <zeebe:header key="timeout" value="PT5M"/>
          <zeebe:header key="retries" value="3"/>
        </zeebe:taskHeaders>
      </extensionElements>
    </serviceTask>
    <scriptTask id="task{branch}_3" scriptFormat="javascript" resultVariable="result{branch}">
      <documentation>Script task documentation with complex business logic description for branch {branch}.</documentation>
      <script>console.log('Branch {branch} processing:', input{branch});</script>
    </scriptTask>
    <businessRuleTask id="task{branch}_4" name="Rule Task {branch}.4">
      <documentation>Business rule task documentation explaining the decision logic for branch {branch}.</documentation>
    </businessRuleTask>
    <sendTask id="task{branch}_5" name="Send Task {branch}.5">
      <documentation>Send task documentation with message format and routing details for branch {branch}.</documentation>
    </sendTask>
    <receiveTask id="task{branch}_6" name="Receive Task {branch}.6">
      <documentation>Receive task documentation with expected message structure for branch {branch}.</documentation>
    </receiveTask>
    <manualTask id="task{branch}_7" name="Manual Task {branch}.7">
      <documentation>Manual task documentation with step-by-step user instructions for branch {branch}.</documentation>
    </manualTask>
    <callActivity id="task{branch}_8" name="Call Activity {branch}.8">
      <documentation>Call activity documentation with subprocess parameters and expected outputs for branch {branch}.</documentation>
    </callActivity>
    <parallelGateway id="join{branch}"/>
""";

            // Add text annotations and associations (artifacts that can be optimized)
            if (branch % 5 == 0) // Add artifacts every 5th branch
            {
                xml += $"""
    <textAnnotation id="annotation{branch}">
      <text>This is a detailed annotation for branch {branch} explaining the business process flow, compliance requirements, and operational procedures.</text>
    </textAnnotation>
    <association id="assoc{branch}" sourceRef="task{branch}_1" targetRef="annotation{branch}"/>
    <group id="group{branch}" categoryValueRef="category{branch}"/>
""";
            }
        }
        
        xml += """
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="fork0"/>
""";
        
        // Generate sequence flows (8 per branch + connectors)
        for (int branch = 0; branch < 50; branch++)
        {
            if (branch > 0)
                xml += $"""    <sequenceFlow id="f{branch * 20}" sourceRef="join{branch - 1}" targetRef="fork{branch}"/>""" + "\n";
                
            xml += $"""
    <sequenceFlow id="f{branch * 20 + 1}" sourceRef="fork{branch}" targetRef="task{branch}_1"/>
    <sequenceFlow id="f{branch * 20 + 2}" sourceRef="task{branch}_1" targetRef="task{branch}_2"/>
    <sequenceFlow id="f{branch * 20 + 3}" sourceRef="task{branch}_2" targetRef="task{branch}_3"/>
    <sequenceFlow id="f{branch * 20 + 4}" sourceRef="task{branch}_3" targetRef="task{branch}_4"/>
    <sequenceFlow id="f{branch * 20 + 5}" sourceRef="task{branch}_4" targetRef="task{branch}_5"/>
    <sequenceFlow id="f{branch * 20 + 6}" sourceRef="task{branch}_5" targetRef="task{branch}_6"/>
    <sequenceFlow id="f{branch * 20 + 7}" sourceRef="task{branch}_6" targetRef="task{branch}_7"/>
    <sequenceFlow id="f{branch * 20 + 8}" sourceRef="task{branch}_7" targetRef="task{branch}_8"/>
    <sequenceFlow id="f{branch * 20 + 9}" sourceRef="task{branch}_8" targetRef="join{branch}"/>
""";
        }
        
        xml += $"""    <sequenceFlow id="fend" sourceRef="join49" targetRef="end"/>""" + "\n";
        xml += """
  </process>
</definitions>
""";
        return xml;
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<BpmnParserPerformanceBenchmarks>();
    }
}