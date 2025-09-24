using System;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictNamespaceAndDocumentationTests
{
    private const string SourceBpmn = @"<bpmn:definitions xmlns:aaa=""http://example.com/aaa"" xmlns:bbb=""http://example.com/bbb"" xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:camunda=""http://camunda.org/schema/1.0/bpmn"" targetNamespace=""http://example.com/test"">
  <bpmn:process id=""proc_ns_doc"" name=""NSDoc"">
    <bpmn:documentation>Process Doc</bpmn:documentation>
    <bpmn:startEvent id=""startA"">
      <bpmn:documentation>Start Doc</bpmn:documentation>
      <bpmn:outgoing>flow1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:endEvent id=""endA"">
      <bpmn:incoming>flow1</bpmn:incoming>
      <bpmn:documentation>End Doc</bpmn:documentation>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""flow1"" sourceRef=""startA"" targetRef=""endA"" />
  </bpmn:process>
</bpmn:definitions>";

    [Fact]
    public void Strict_Roundtrip_Preserves_Namespace_Order_And_Documentation()
    {
        var parser = new BpmnParser(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });
        var model = parser.ParseAsync(SourceBpmn).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        Assert.NotNull(model.RawMetadata!.NamespacePrefixes);
        var xmlOut = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);

        var iAaa = xmlOut.IndexOf("xmlns:aaa=\"http://example.com/aaa\"");
        var iBbb = xmlOut.IndexOf("xmlns:bbb=\"http://example.com/bbb\"");
        var iBpmn = xmlOut.IndexOf("xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"");
        var iCamunda = xmlOut.IndexOf("xmlns:camunda=\"http://camunda.org/schema/1.0/bpmn\"");
        Assert.True(iAaa >= 0 && iBbb > iAaa && iBpmn > iBbb && iCamunda > iBpmn, "Namespace prefix order not preserved");

        Assert.Contains("<bpmn:documentation>Process Doc</bpmn:documentation>", xmlOut);
        Assert.Contains("<bpmn:documentation>Start Doc</bpmn:documentation>", xmlOut);
        Assert.Contains("<bpmn:documentation>End Doc</bpmn:documentation>", xmlOut);
    }
}
