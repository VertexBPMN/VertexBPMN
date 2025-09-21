using System.Linq;
using Xunit;
using System.Threading.Tasks;
using VertexBPMN.Parsing;

namespace VertexBPMN.Test.Parsing;

public class SequenceFlowParsingTests
{
    private readonly UnifiedBpmnParser _parser = new();

    [Fact]
    public async Task Parses_Condition_And_Default_Flow()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <exclusiveGateway id='g1' default='flow2'/>
    <sequenceFlow id='flow1' sourceRef='g1' targetRef='taskA'>
      <conditionExpression><![CDATA[${x > 5}]]></conditionExpression>
    </sequenceFlow>
    <sequenceFlow id='flow2' sourceRef='g1' targetRef='taskB'/>
    <userTask id='taskA'/>
    <userTask id='taskB'/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        Assert.Equal(2, model.SequenceFlows.Count);
        var cond = model.SequenceFlows.First(f => f.Id == "flow1");
        Assert.Equal("${x > 5}", cond.ConditionExpression);
        var def = model.SequenceFlows.First(f => f.Id == "flow2");
        Assert.True(def.IsDefault);
    }
}
