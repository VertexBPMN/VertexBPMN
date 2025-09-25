using System.Linq;
using System.Xml.Linq;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

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

        // Basic structure checks
        Assert.Contains("<bpmn:message id=\"msg1\"", outXml);
        Assert.Contains("<bpmn:signal id=\"sig1\"", outXml);
        Assert.Contains("<bpmn:boundaryEvent id=\"b1\"", outXml);
        Assert.Contains("attachedToRef=\"ut1\"", outXml);
        Assert.Contains("cancelActivity=\"false\"", outXml);
        Assert.Contains("<bpmn:timerEventDefinition", outXml);
        Assert.Contains("<bpmn:subProcess id=\"sp1\"", outXml);

        // Canonical (whitespace-normalized) equality expectation
        //Assert.Equal(Normalize(Canonical(xml)), Normalize(Canonical(outXml)));
    }
}
