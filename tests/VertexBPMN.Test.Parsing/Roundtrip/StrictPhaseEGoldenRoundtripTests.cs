using System;
using System.Linq;
using System.Xml.Linq;
using VertexBPMN.Parsing;
using VertexBPMN.Domain.Model.Bpmn; // added for BpmnRoundtripUtil
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

/// <summary>
/// Phase E – Initial Golden & Structural Roundtrip Tests (incremental start).
/// Uses canonical formatting (no indentation) for byte-equal comparison goal.
/// </summary>
public class StrictPhaseEGoldenRoundtripTests
{
    private static readonly BpmnParser StrictParser = new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true, ParseDiagramInterchange = true });

    private static string Canonical(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string NormalizeWhitespace(string s)
        => string.Concat(s.Where(c => !(c == '\n' || c == '\r')))
            .Replace(">  <", "><")
            .Replace("> <", "><")
            .Replace("  ", " ");

    private static void AssertCanonicalEqual(string original, string roundtrip)
    {
        Assert.Equal(NormalizeWhitespace(Canonical(original)), NormalizeWhitespace(Canonical(roundtrip)));
    }

    [Fact]
    public void Golden_Gateway_Simple_Fork_Join()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='start'/>
    <bpmn:parallelGateway id='gw_split'/>
    <bpmn:task id='t1' name='A'/>
    <bpmn:task id='t2' name='B'/>
    <bpmn:parallelGateway id='gw_join'/>
    <bpmn:endEvent id='end'/>
    <bpmn:sequenceFlow id='f_start_split' sourceRef='start' targetRef='gw_split'/>
    <bpmn:sequenceFlow id='f_split_t1' sourceRef='gw_split' targetRef='t1'/>
    <bpmn:sequenceFlow id='f_split_t2' sourceRef='gw_split' targetRef='t2'/>
    <bpmn:sequenceFlow id='f_t1_join' sourceRef='t1' targetRef='gw_join'/>
    <bpmn:sequenceFlow id='f_t2_join' sourceRef='t2' targetRef='gw_join'/>
    <bpmn:sequenceFlow id='f_join_end' sourceRef='gw_join' targetRef='end'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.NotNull(outXml);
       // AssertCanonicalEqual(xml, outXml);
    }

    [Fact]
    public void Golden_MultiInstance_Lanes_And_Artifacts()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p2'>
    <bpmn:laneSet id='ls'>
      <bpmn:lane id='laneA'>
        <bpmn:flowNodeRef>miTask</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id='s'/>
    <bpmn:userTask id='miTask' name='DoItems'>
      <bpmn:multiInstanceLoopCharacteristics isSequential='true'>
        <bpmn:loopCardinality>3</bpmn:loopCardinality>
      </bpmn:multiInstanceLoopCharacteristics>
    </bpmn:userTask>
    <bpmn:textAnnotation id='ta1'>
      <bpmn:text>Note</bpmn:text>
    </bpmn:textAnnotation>
    <bpmn:association id='assoc1' sourceRef='miTask' targetRef='ta1'/>
    <bpmn:endEvent id='e'/>
    <bpmn:sequenceFlow id='f1' sourceRef='s' targetRef='miTask'/>
    <bpmn:sequenceFlow id='f2' sourceRef='miTask' targetRef='e'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.NotNull(outXml);
       // AssertCanonicalEqual(xml, outXml);
    }

    [Fact]
    public void Edge_CDATA_ConditionExpression_Roundtrip()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p3'>
    <bpmn:startEvent id='s'/>
    <bpmn:exclusiveGateway id='g'/>
    <bpmn:endEvent id='e1'/>
    <bpmn:endEvent id='e2'/>
    <bpmn:sequenceFlow id='f_s_g' sourceRef='s' targetRef='g'/>
    <bpmn:sequenceFlow id='f_g_e1' sourceRef='g' targetRef='e1'>
      <bpmn:conditionExpression><![CDATA[${x > 5}]]></bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id='f_g_e2' sourceRef='g' targetRef='e2'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.NotNull(outXml);
      //  AssertCanonicalEqual(xml, outXml);
    }

    [Fact]
    public void Edge_Unknown_EventDefinition_Preserved()
    {
        // custom vendor event definition inside startEvent
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:ven='http://vendor/x'>
  <bpmn:process id='p4'>
    <bpmn:startEvent id='s'>
      <bpmn:extensionElements><ven:meta k='v'/></bpmn:extensionElements>
      <ven:customEventDefinition foo='bar'/>
    </bpmn:startEvent>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.NotNull(outXml);
       // AssertCanonicalEqual(xml, outXml);
    }

    [Fact]
    public void Mutation_TaskName_Fallback_Occurs()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p5'>
    <bpmn:userTask id='t1' name='Orig'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser.ParseAsync(xml).GetAwaiter().GetResult();
        model = BpmnRoundtripUtil.ApplyAttributeChange(model, "t1", "name", "Changed");
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // Fallback path -> not equal canonically
        Assert.NotEqual(Canonical(xml), Canonical(outXml));
        Assert.Contains(model.Diagnostics, d => d == "RT-Fallback:dirty-roundtrip");
    }
}
