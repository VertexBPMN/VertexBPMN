using System;
using System.Linq;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhaseCAdditionalSerializerTests
{
    private static BpmnParser P => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void Strict_Retains_Unknown_Vendor_EventDefinition_Raw()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:x='http://vendor/custom'>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s1'>
      <x:customEventDefinition foo='bar'/>
    </bpmn:startEvent>
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.Contains("<x:customEventDefinition", outXml);
        Assert.Contains("foo=\"bar\"", outXml);
    }

    [Fact]
    public void Strict_Retains_MultiInstance_Loop_Node_Unmodified()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1'>
      <bpmn:multiInstanceLoopCharacteristics isSequential='true'>
        <bpmn:loopCardinality>5</bpmn:loopCardinality>
      </bpmn:multiInstanceLoopCharacteristics>
    </bpmn:userTask>
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // ensure same cardinality and isSequential attribute present exactly once
        Assert.Contains("<bpmn:multiInstanceLoopCharacteristics", outXml);
        Assert.Contains("isSequential='true'".Replace('\'', '"'), outXml.Replace('\'', '"'));
        var idx = outXml.IndexOf("<bpmn:multiInstanceLoopCharacteristics", StringComparison.Ordinal);
        Assert.True(idx >= 0);
        var section = outXml.Substring(idx, Math.Min(200, outXml.Length - idx));
        Assert.Equal(1, section.Split("isSequential").Length - 1);
    }

    [Fact]
    public void Strict_DoesNot_Generate_Incoming_Outgoing_When_Disabled_And_Missing_In_Original()
    {
        // sequenceFlow present but no incoming/outgoing elements on the flow nodes in original
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s1'/>
    <bpmn:userTask id='t1'/>
    <bpmn:sequenceFlow id='f1' sourceRef='s1' targetRef='t1'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        var serializer = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict, PreserveGeneratedIfMissing = false };
        var outXml = serializer.Serialize(model);
        Assert.DoesNotContain("<bpmn:incoming>f1</bpmn:incoming>", outXml);
        Assert.DoesNotContain("<bpmn:outgoing>f1</bpmn:outgoing>", outXml);
    }
}
