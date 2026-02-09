using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhaseEPriorityNamespaceTests
{
    private static BpmnParser P => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void PriorityAttribute_VertexNamespace_Preserved()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:vertex='http://vertexbpmn.io/schema/1.0'>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s'/>
    <bpmn:task id='t'/>
    <bpmn:sequenceFlow id='f1' sourceRef='s' targetRef='t' vertex:priority='7'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.Contains("vertex:priority=\"7\"", outXml);
    }

    [Fact]
    public void PriorityAttribute_CamundaNamespace_Preserved()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn'>
  <bpmn:process id='p2'>
    <bpmn:startEvent id='s'/>
    <bpmn:task id='t'/>
    <bpmn:sequenceFlow id='f2' sourceRef='s' targetRef='t' camunda:priority='5'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.Contains("camunda:priority=\"5\"", outXml);
    }
}
