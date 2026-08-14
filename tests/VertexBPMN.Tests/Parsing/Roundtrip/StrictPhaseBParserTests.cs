using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

/// <summary>
/// Phase B incremental tests (TDD) – ensures new parser options & diagnostics.
/// </summary>
public class StrictPhaseBParserTests
{
    private static BpmnParser CreateStrict(BpmnParserOptions? opt = null)
        => new(opt ?? new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true, ParseDiagramInterchange = true });

    [Fact]
    public void Missing_Id_On_FlowNode_Produces_Diagnostic()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:userTask name='NoIdTask' />
  </bpmn:process>
</bpmn:definitions>";
        var model = CreateStrict().ParseAsync(xml).GetAwaiter().GetResult();
        Assert.Contains(model.Diagnostics, d => d.Contains("Missing id on userTask"));
    }

    [Fact]
    public void CaptureArtifacts_False_Disables_RawArtifacts()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:textAnnotation id='ta1'>
      <bpmn:text>A</bpmn:text>
    </bpmn:textAnnotation>
  </bpmn:process>
</bpmn:definitions>";
        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true, CaptureArtifacts = false };
        var model = CreateStrict(options).ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata); // still strict
        Assert.True(model.RawMetadata!.RawArtifacts == null || model.RawMetadata.RawArtifacts.Count == 0);
    }

    [Fact]
    public void CaptureArtifacts_True_Captures_RawArtifacts()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:textAnnotation id='ta1'>
      <bpmn:text>A</bpmn:text>
    </bpmn:textAnnotation>
  </bpmn:process>
</bpmn:definitions>";
        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true, CaptureArtifacts = true };
        var model = CreateStrict(options).ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        Assert.NotNull(model.RawMetadata!.RawArtifacts);
        Assert.Single(model.RawMetadata.RawArtifacts!);
    }

    [Fact]
    public void CaptureDiRaw_False_Suppresses_RawDiRoot()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:bpmndi='http://www.omg.org/spec/BPMN/20100524/DI' xmlns:omgdc='http://www.omg.org/spec/DD/20100524/DC' xmlns:omgdi='http://www.omg.org/spec/DD/20100524/DI'>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s1'/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id='D1'>
    <bpmndi:BPMNPlane bpmnElement='p1'>
      <bpmndi:BPMNShape id='S_s1' bpmnElement='s1'>
        <omgdc:Bounds x='10' y='10' width='36' height='36'/>
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";
        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true, ParseDiagramInterchange = true, CaptureDiRaw = false };
        var model = CreateStrict(options).ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        Assert.Null(model.RawMetadata!.RawDiRoot);
        // Shapes still parsed because ParseDiagramInterchange true
        Assert.NotNull(model.Shapes);
        Assert.Single(model.Shapes!);
    }
}
