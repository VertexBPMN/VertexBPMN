using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class StrictPhaseEGoldenAdvancedTests
{
    private static readonly BpmnParser StrictParser = new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });
    private static readonly XNamespace BPMN = "http://www.omg.org/spec/BPMN/20100524/MODEL";

    private static string Canonical(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string Normalize(string s) => string.Concat(s.Where(c => c!='\n' && c!='\r'));

    [Fact]
    public void Golden_Subprocess_Boundary_Signal_Message()
    {
        // Subprocess kept empty (serializer currently emits no nested flow nodes) + boundary timer event attached to user task + global message/signal
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:message id='msg1'/>
  <bpmn:signal id='sig1'/>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s1'/>
    <bpmn:userTask id='ut1' name='Work'/>
    <bpmn:boundaryEvent id='b1' attachedToRef='ut1' cancelActivity='false'>
      <bpmn:timerEventDefinition/>
    </bpmn:boundaryEvent>
    <bpmn:subProcess id='sp1'/>
    <bpmn:endEvent id='e1'/>
    <bpmn:sequenceFlow id='f1' sourceRef='s1' targetRef='ut1'/>
    <bpmn:sequenceFlow id='f2' sourceRef='ut1' targetRef='e1'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);

        var document = XDocument.Parse(outXml);
        Assert.Equal("msg1", (string?)document.Descendants(BPMN + "message").Single().Attribute("id"));
        Assert.Equal("sig1", (string?)document.Descendants(BPMN + "signal").Single().Attribute("id"));
        var boundaryEvent = document.Descendants(BPMN + "boundaryEvent").Single();
        Assert.Equal("b1", (string?)boundaryEvent.Attribute("id"));
        Assert.Equal("ut1", (string?)boundaryEvent.Attribute("attachedToRef"));
        Assert.Equal("false", (string?)boundaryEvent.Attribute("cancelActivity"));
        Assert.Single(boundaryEvent.Elements(BPMN + "timerEventDefinition"));
        Assert.Equal("sp1", (string?)document.Descendants(BPMN + "subProcess").Single().Attribute("id"));

        Assert.Equal(Normalize(Canonical(xml)), Normalize(Canonical(outXml)));
    }
}
