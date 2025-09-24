using System;
using System.Linq;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictSerializerRoundtripTests
{
    private static BpmnParser StrictParser() => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void Strict_Roundtrip_Preserves_Raw_Structures()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn' targetNamespace='http://example.local/test'>
  <bpmn:message id='Msg_A' name='A'/>
  <bpmn:signal id='Sig_X' name='X'/>
  <bpmn:process id='proc_struct'>
    <bpmn:documentation>Proc Doc</bpmn:documentation>
    <bpmn:laneSet id='ls1'>
      <bpmn:lane id='lane_main'>
        <bpmn:flowNodeRef>task_loop</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id='start_1' isInterrupting='false'/>
    <bpmn:userTask id='task_loop' name='Loop Task'>
      <bpmn:multiInstanceLoopCharacteristics isSequential='true' camunda:collection='items' camunda:elementVariable='item'>
        <bpmn:loopCardinality>3</bpmn:loopCardinality>
      </bpmn:multiInstanceLoopCharacteristics>
    </bpmn:userTask>
    <bpmn:textAnnotation id='ta1'>
      <bpmn:text>Hello</bpmn:text>
    </bpmn:textAnnotation>
    <bpmn:association id='assoc1' sourceRef='task_loop' targetRef='ta1'/>
    <bpmn:sequenceFlow id='flow1' sourceRef='start_1' targetRef='task_loop' camunda:priority='25'/>
  </bpmn:process>
</bpmn:definitions>";

        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);

        var idxMsg = outXml.IndexOf("<bpmn:message id=\"Msg_A\"");
        var idxProc = outXml.IndexOf("<bpmn:process id=\"proc_struct\"");
        Assert.True(idxMsg >= 0 && idxProc > idxMsg, "Message should precede process in strict output");

        Assert.Contains("<bpmn:multiInstanceLoopCharacteristics", outXml);
        Assert.Contains("isSequential='true'", outXml.Replace('"','\''));
        Assert.Contains("camunda:collection=\"items\"", outXml);
        Assert.Contains("<bpmn:loopCardinality>3</bpmn:loopCardinality>", outXml);

        Assert.Contains("camunda:priority=\"25\"", outXml);
        Assert.DoesNotContain("vertex:priority=\"25\"", outXml);

        Assert.Contains("<bpmn:laneSet id=\"ls1\"", outXml);
        Assert.Contains("<bpmn:lane id=\"lane_main\"", outXml);

        Assert.Contains("<bpmn:textAnnotation id=\"ta1\"", outXml);
        Assert.Contains("<bpmn:association id=\"assoc1\"", outXml);

        Assert.Contains("<bpmn:startEvent id=\"start_1\"", outXml);
        Assert.Contains("isInterrupting=\"false\"", outXml);

        Assert.Contains("<bpmn:documentation>Proc Doc</bpmn:documentation>", outXml);
    }

    [Fact]
    public void Strict_Roundtrip_Preserves_Task_Name_And_Does_Not_Add_When_Missing()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p2'>
    <bpmn:userTask id='t_with' name='HasName'/>
    <bpmn:userTask id='t_without'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);

        // Extract specific element snippets to avoid cross-line collisions (serializer is single-line)
        string ExtractElement(string id)
        {
            var anchor = $"<bpmn:userTask id=\"{id}\"";
            var idx = outXml.IndexOf(anchor, StringComparison.Ordinal);
            Assert.True(idx >= 0, $"Element {id} not found");
            var close = outXml.IndexOf("/>", idx, StringComparison.Ordinal);
            Assert.True(close > idx, "Self-closing terminator not found for userTask");
            return outXml.Substring(idx, close - idx + 2);
        }

        var withSnippet = ExtractElement("t_with");
        Assert.Contains("name=\"HasName\"", withSnippet);

        var withoutSnippet = ExtractElement("t_without");
        Assert.DoesNotContain("name=\"", withoutSnippet);
    }

    [Fact]
    public void Strict_Roundtrip_Replays_SequenceFlow_Condition_CData_State()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p3'>
    <bpmn:startEvent id='s'/>
    <bpmn:userTask id='t'/>
    <bpmn:sequenceFlow id='f' sourceRef='s' targetRef='t'>
      <bpmn:conditionExpression><![CDATA[${x > 5}]]></bpmn:conditionExpression>
    </bpmn:sequenceFlow>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.Contains("<![CDATA[${x > 5}]]>", outXml);
    }
}
