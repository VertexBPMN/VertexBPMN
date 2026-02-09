using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Ecosystem;
using VertexBPMN.Engine.Parsing;

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


/// <summary>
/// Test implementation of custom vendor handler.
/// </summary>
public class TestCustomVendorHandler : IBpmnVendorExtensionInterpreter
{
    public bool WasInvoked { get; private set; }
    public List<string> ProcessedElements { get; } = new();

    public string[] SupportedNamespaces => new[] { "http://test.vendor/extensions" };

    public bool CanHandle(string namespaceUri, string localName)
    {
        return namespaceUri == "http://test.vendor/extensions";
    }

    public VendorExtensionResult ProcessExtension(XElement element, string ownerElementId)
    {
        WasInvoked = true;
        ProcessedElements.Add($"{element.Name.LocalName}");

        var result = new VendorExtensionResult();

        if (element.Name.LocalName == "specialProcessor")
        {
            result.NormalizedAttributes["custom:processor.type"] = element.Attribute("type")?.Value ?? "";
        }
        else if (element.Name.LocalName == "config")
        {
            foreach (var attr in element.Attributes())
            {
                result.NormalizedAttributes[$"custom:config.{attr.Name.LocalName}"] = attr.Value;
            }
        }

        return result;
    }
}

/// <summary>
/// Test implementation of namespace-specific vendor handler.
/// </summary>
public class NamespaceSpecificVendorHandler : IBpmnVendorExtensionInterpreter
{
    private readonly string _targetNamespace;

    public NamespaceSpecificVendorHandler(string targetNamespace)
    {
        _targetNamespace = targetNamespace;
    }

    public bool WasInvoked { get; private set; }
    public string? ProcessedNamespace { get; private set; }

    public string[] SupportedNamespaces => new[] { _targetNamespace };

    public bool CanHandle(string namespaceUri, string localName)
    {
        return namespaceUri == _targetNamespace;
    }

    public VendorExtensionResult ProcessExtension(XElement element, string ownerElementId)
    {
        WasInvoked = true;
        ProcessedNamespace = element.Name.NamespaceName;

        return new VendorExtensionResult
        {
            NormalizedAttributes = { [$"{element.Name.LocalName}.processed"] = "true" }
        };
    }
}