using System.Linq;
using System.Threading.Tasks;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Runtime;

public class RuntimeProjectionAdvancedTests
{
    private readonly BpmnParser _parser = new(new BpmnParserOptions {
        RoundtripMode = BpmnRoundtripMode.Strict,
        BuildRuntimeProjection = true,
        NormalizeVendorExtensions = true
    });

    [Fact]
    public async Task DefaultFlow_TargetNode_FlagIsSet()
    {
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="p1">
    <startEvent id="start"/>
    <exclusiveGateway id="gw1" default="f2"/>
    <userTask id="taskA"/>
    <userTask id="taskB"/>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="gw1"/>
    <sequenceFlow id="f2" sourceRef="gw1" targetRef="taskA"/>
    <sequenceFlow id="f3" sourceRef="gw1" targetRef="taskB">
      <conditionExpression><![CDATA[${x > 0}]]></conditionExpression>
    </sequenceFlow>
    <sequenceFlow id="f4" sourceRef="taskA" targetRef="end"/>
    <sequenceFlow id="f5" sourceRef="taskB" targetRef="end"/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var rt = model.Runtime!;
        var taskA = rt.FlowNodes.Single(n => n.Id == "taskA");
        var taskB = rt.FlowNodes.Single(n => n.Id == "taskB");

        Assert.True(taskA.IsDefaultGatewayTarget);
        Assert.False(taskB.IsDefaultGatewayTarget);
    }

    [Fact]
    public async Task MultiInstance_Task_FlagsDetected_SequentialAndParallel()
    {
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="p1">
    <startEvent id="start"/>
    <userTask id="miSeq">
      <multiInstanceLoopCharacteristics isSequential="true">
        <loopCardinality>5</loopCardinality>
      </multiInstanceLoopCharacteristics>
    </userTask>
    <serviceTask id="miPar">
      <multiInstanceLoopCharacteristics>
        <loopCardinality>3</loopCardinality>
      </multiInstanceLoopCharacteristics>
    </serviceTask>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="miSeq"/>
    <sequenceFlow id="f2" sourceRef="miSeq" targetRef="miPar"/>
    <sequenceFlow id="f3" sourceRef="miPar" targetRef="end"/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var rt = model.Runtime!;

        var miSeq = rt.FlowNodes.Single(n => n.Id == "miSeq");
        var miPar = rt.FlowNodes.Single(n => n.Id == "miPar");

        Assert.True(miSeq.IsMultiInstance);
        Assert.True(miSeq.IsMultiInstanceSequential);

        Assert.True(miPar.IsMultiInstance);
        Assert.False(miPar.IsMultiInstanceSequential);
    }
}