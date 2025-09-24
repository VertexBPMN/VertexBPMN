using System;
using System.Linq;
using System.Xml.Linq;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhaseCSerializerTests
{
    private BpmnParser StrictParser() => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void Strict_Appends_New_Extension_Namespace_Prefixes_At_End()
    {
        // definitions has only aaa + bbb; extensionElements introduces new prefix 'x'
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:aaa='http://example.com/aaa' xmlns:bbb='http://example.com/bbb'>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s1'>
      <bpmn:extensionElements>
        <x:foo xmlns:x='http://example.com/x' attr='v'/>
      </bpmn:extensionElements>
    </bpmn:startEvent>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);

        var idxAaa = outXml.IndexOf("xmlns:aaa=\"http://example.com/aaa\"");
        var idxBbb = outXml.IndexOf("xmlns:bbb=\"http://example.com/bbb\"");
        Assert.True(idxAaa >= 0 && idxBbb > idxAaa, "Original prefix order broken");
        // new prefix must appear and after original ones
        var idxX = outXml.IndexOf("xmlns:x=\"http://example.com/x\"");
        Assert.True(idxX > idxBbb, "New extension prefix not appended after originals");
    }

    [Fact]
    public void Strict_Preserves_FlowNode_Order_By_OrderIndex()
    {
        // Intentionally unusual order: task before startEvent, then gateway, then sequenceFlows
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:task id='t1'/>
    <bpmn:startEvent id='s1'/>
    <bpmn:exclusiveGateway id='g1'/>
    <bpmn:sequenceFlow id='f1' sourceRef='s1' targetRef='t1'/>
    <bpmn:sequenceFlow id='f2' sourceRef='t1' targetRef='g1'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // Extract ordering of elements inside process (ignore attributes)
        var processStart = outXml.IndexOf("<bpmn:process id=\"p1\"");
        Assert.True(processStart >= 0);
        var processClose = outXml.IndexOf("</bpmn:process>", processStart, StringComparison.Ordinal);
        var inner = outXml.Substring(processStart, processClose - processStart);
        int posTask = inner.IndexOf("<bpmn:task id=\"t1\"", StringComparison.Ordinal);
        int posStart = inner.IndexOf("<bpmn:startEvent id=\"s1\"", StringComparison.Ordinal);
        int posGateway = inner.IndexOf("<bpmn:exclusiveGateway id=\"g1\"", StringComparison.Ordinal);
        int posF1 = inner.IndexOf("<bpmn:sequenceFlow id=\"f1\"", StringComparison.Ordinal);
        int posF2 = inner.IndexOf("<bpmn:sequenceFlow id=\"f2\"", StringComparison.Ordinal);
        Assert.True(posTask < posStart && posStart < posGateway && posGateway < posF1 && posF1 < posF2, "Element order not preserved");
    }
}
