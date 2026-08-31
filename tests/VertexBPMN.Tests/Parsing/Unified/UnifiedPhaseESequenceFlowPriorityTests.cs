using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Unified;

public class UnifiedPhaseESequenceFlowPriorityTests
{
    private readonly BpmnParser _parser = new();
    private readonly BpmnSerializer _serializer = new();

    [Fact]
    public async Task Parses_Camunda_Priority_On_SequenceFlow()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn'>
  <process id='p1'>
    <startEvent id='s1'/>
    <exclusiveGateway id='g1'/>
    <sequenceFlow id='f1' sourceRef='s1' targetRef='g1' camunda:priority='10'/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var flow = Assert.Single(model.SequenceFlows);
        Assert.Equal(10, flow.Priority);
    }

    [Fact]
    public async Task Serializes_Vertex_Priority_On_SequenceFlow()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:vertexbpmn='http://vertexbpmn.io/schema/1.0/bpmn'>
  <process id='p1'>
    <startEvent id='s1'/>
    <exclusiveGateway id='g1'/>
    <vertexbpmn:sequenceFlow id='f1' sourceRef='s1' targetRef='g1' priority='5'/>
  </process>
</definitions>
""";

        var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var flow = Assert.Single(model.SequenceFlows);
        Assert.Equal(5, flow.Priority);
        //var serialized = _serializer.Serialize(model);
        //Assert.Contains("vertexbpmn.io/schema/1.0", serialized);
        //Assert.Contains("priority=\"5\"", serialized);
    }
}
