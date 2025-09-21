using System.Threading.Tasks;
using System.Linq;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Unified;

public class UnifiedPhaseESequenceFlowPriorityTests
{
    private readonly UnifiedBpmnParser _parser = new();
    private readonly UnifiedBpmnSerializer _serializer = new();

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
        var model = await _parser.ParseAsync(xml);
        var flow = Assert.Single(model.SequenceFlows);
        Assert.Equal(10, flow.Priority);
    }

    [Fact]
    public async Task Serializes_Vertex_Priority_On_SequenceFlow()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <startEvent id='s1'/>
    <exclusiveGateway id='g1'/>
    <sequenceFlow id='f1' sourceRef='s1' targetRef='g1' priority='5'/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var serialized = _serializer.Serialize(model);
        Assert.Contains("vertexbpmn.io/schema/1.0", serialized);
        Assert.Contains("priority=\"5\"", serialized);
    }
}
