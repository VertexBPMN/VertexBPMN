using Xunit;
using System.Threading.Tasks;
using VertexBPMN.Domain.Model.Bpmn.Model;
using VertexBPMN.Parsing;

namespace VertexBPMN.Test.Parsing;

public class LoopCharacteristicsParsingTests
{
    private readonly UnifiedBpmnParser _parser = new();

    [Fact]
    public async Task Parses_MultiInstance_Sequential()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <subProcess id='sp1'>
      <multiInstanceLoopCharacteristics isSequential='true'>
        <loopCardinality>5</loopCardinality>
        <completionCondition>nrOfCompletedInstances/nrOfInstances == 1</completionCondition>
      </multiInstanceLoopCharacteristics>
    </subProcess>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var sp = Assert.Single(model.Subprocesses);
        var mi = Assert.IsType<MultiInstanceLoopCharacteristics>(sp.Loop);
        Assert.True(mi.IsSequential);
        Assert.Equal(5, mi.LoopCardinality);
        Assert.Equal("nrOfCompletedInstances/nrOfInstances == 1", mi.CompletionCondition);
    }

    [Fact]
    public async Task Parses_Standard_Loop()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1' name='Test Process'>
    <subProcess id='sp1'>
      <standardLoopCharacteristics testBefore='true' loopMaximum='9'>
        <loopCondition><![CDATA[${i < 10}]]></loopCondition>
      </standardLoopCharacteristics>
    </subProcess>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var sp = Assert.Single(model.Subprocesses);
        var loop = Assert.IsType<StandardLoopCharacteristics>(sp.Loop);
        Assert.True(loop.TestBefore);
        Assert.Equal(9, loop.LoopMaximum);
        Assert.Equal("${i < 10}", loop.LoopCondition);
    }
}
