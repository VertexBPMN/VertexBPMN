using System.Threading.Tasks;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Unified;

public class UnifiedParserSerializerTests
{
    private readonly BpmnParser _parser = new();
    private readonly BpmnSerializer _serializer = new();

    [Fact]
    public async Task Roundtrip_Preserves_EventDefinitions_And_DefaultFlow()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <startEvent id='s1'>
      <timerEventDefinition><timeDuration>PT2M</timeDuration></timerEventDefinition>
    </startEvent>
    <exclusiveGateway id='g1' default='f2'/>
    <sequenceFlow id='f1' sourceRef='g1' targetRef='t1'>
      <conditionExpression>${x > 1}</conditionExpression>
    </sequenceFlow>
    <sequenceFlow id='f2' sourceRef='g1' targetRef='t2'/>
    <userTask id='t1'/>
    <userTask id='t2'/>
    <endEvent id='e1'>
      <signalEventDefinition signalRef='SIG_X'/>
    </endEvent>
    <sequenceFlow id='f3' sourceRef='t2' targetRef='e1'/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var serialized = _serializer.Serialize(model);
        Assert.Contains("timerEventDefinition", serialized);
        Assert.Contains("signalEventDefinition", serialized);
        Assert.Contains("exclusiveGateway id=\"g1\" default=\"f2\"", serialized);
        Assert.Contains("conditionExpression", serialized);
    }
}
