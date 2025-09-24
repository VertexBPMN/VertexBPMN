using System.Linq;
using System.Xml.Linq;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhaseAUpgradesTests
{
    private static BpmnParser CreateStrictParser() => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void Captures_RawMultiInstance_And_PriorityNamespace_And_FlowNodeAttributes()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='taskA' name='User Task 1'>
      <bpmn:multiInstanceLoopCharacteristics isSequential='true'>
        <bpmn:loopCardinality>3</bpmn:loopCardinality>
      </bpmn:multiInstanceLoopCharacteristics>
    </bpmn:userTask>
    <bpmn:sequenceFlow id='f1' sourceRef='taskA' targetRef='taskA' camunda:priority='5'/>
  </bpmn:process>
</bpmn:definitions>";
        var parser = CreateStrictParser();
        var model = parser.ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        // RawMultiInstance
        Assert.NotNull(model.RawMetadata!.RawMultiInstance);
        Assert.True(model.RawMetadata.RawMultiInstance!.ContainsKey("taskA"));
        Assert.Equal("multiInstanceLoopCharacteristics", model.RawMetadata.RawMultiInstance!["taskA"].Name.LocalName);
        // PriorityAttributeNamespace
        Assert.NotNull(model.RawMetadata.PriorityAttributeNamespace);
        Assert.Equal("http://camunda.org/schema/1.0/bpmn", model.RawMetadata.PriorityAttributeNamespace!["f1"]);
        // FlowNodeAttributes include name
        Assert.NotNull(model.RawMetadata.FlowNodeAttributes);
        Assert.True(model.RawMetadata.FlowNodeAttributes!.ContainsKey("taskA"));
        Assert.Contains("name", model.RawMetadata.FlowNodeAttributes!["taskA"].Keys);
    }

    [Fact]
    public void Captures_Boundary_And_StartEvent_Interrupting_Flags()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <subProcess id='sp1'>
      <startEvent id='start1' isInterrupting='false'/>
      <userTask id='t1'/>
      <boundaryEvent id='b1' attachedToRef='t1' cancelActivity='false'>
        <timerEventDefinition />
      </boundaryEvent>
    </subProcess>
  </process>
</definitions>";
        var parser = CreateStrictParser();
        var model = parser.ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        var attrs = model.RawMetadata!.FlowNodeAttributes!;
        Assert.True(attrs.ContainsKey("start1"));
        Assert.Equal("false", attrs["start1"].First(kv => kv.Key.EndsWith("isInterrupting")).Value);
        Assert.True(attrs.ContainsKey("b1"));
        Assert.Equal("false", attrs["b1"].First(kv => kv.Key.EndsWith("cancelActivity")).Value);
    }

    [Fact]
    public void Captures_LaneSet_And_Lane()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <laneSet id='ls1'>
      <lane id='lane1'>
        <flowNodeRef>t1</flowNodeRef>
      </lane>
    </laneSet>
    <userTask id='t1'/>
  </process>
</definitions>";
        var parser = CreateStrictParser();
        var model = parser.ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        Assert.NotNull(model.RawMetadata!.RawLanes);
        // Should contain laneSet and lane
        Assert.Contains(model.RawMetadata.RawLanes!, x => x.Name.LocalName == "laneSet");
        Assert.Contains(model.RawMetadata.RawLanes!, x => x.Name.LocalName == "lane");
    }

    [Fact]
    public void Propagates_Task_Name_Property()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <userTask id='t1' name='Do Work'/>
  </process>
</definitions>";
        var parser = CreateStrictParser();
        var model = parser.ParseAsync(xml).GetAwaiter().GetResult();
        var task = Assert.Single(model.Tasks);
        Assert.Equal("Do Work", task.Name);
        Assert.NotNull(model.RawMetadata);
        Assert.True(model.RawMetadata!.FlowNodeAttributes!.ContainsKey("t1"));
    }
}
