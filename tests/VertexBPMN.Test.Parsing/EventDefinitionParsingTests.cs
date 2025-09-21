using Xunit;
using System.Threading.Tasks;
using VertexBPMN.Domain.Model.Bpmn.Model;
using VertexBPMN.Parsing;

namespace VertexBPMN.Test.Parsing;

public class EventDefinitionParsingTests
{
    private readonly UnifiedBpmnParser _parser = new();

    [Fact]
    public async Task Parses_Timer_Event_Definitions()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <startEvent id='start_timer'>
      <timerEventDefinition>
        <timeDuration>PT5M</timeDuration>
      </timerEventDefinition>
    </startEvent>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var ev = Assert.Single(model.Events);
        var def = Assert.Single(ev.Definitions);
        Assert.Equal("timer", def.Kind);
        Assert.Equal("PT5M", ((TimerEventDefinition)def).TimeDuration);
    }

    [Fact]
    public async Task Parses_Message_Start_Event()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <startEvent id='start_msg'>
      <messageEventDefinition messageRef='Msg_A' />
    </startEvent>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var ev = Assert.Single(model.Events);
        var def = Assert.Single(ev.Definitions);
        var msg = Assert.IsType<MessageEventDefinition>(def);
        Assert.Equal("Msg_A", msg.MessageRef);
    }

    [Fact]
    public async Task Parses_Signal_And_Error_End_Events()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <endEvent id='end_signal'>
      <signalEventDefinition signalRef='Sig1'/>
    </endEvent>
    <endEvent id='end_error'>
      <errorEventDefinition errorRef='Err42'/>
    </endEvent>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        Assert.Equal(2, model.Events.Count);
        var signal = model.Events[0].Definitions[0] as SignalEventDefinition;
        Assert.Equal("Sig1", signal!.SignalRef);
        var error = model.Events[1].Definitions[0] as ErrorEventDefinition;
        Assert.Equal("Err42", error!.ErrorRef);
    }

    [Fact]
    public async Task Parses_Conditional_Intermediate()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <intermediateCatchEvent id='cond1'>
      <conditionalEventDefinition>
        <conditionExpression><![CDATA[${x > 10}]]></conditionExpression>
      </conditionalEventDefinition>
    </intermediateCatchEvent>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var ev = Assert.Single(model.Events);
        var cond = Assert.IsType<ConditionalEventDefinition>(ev.Definitions[0]);
        Assert.Equal("${x > 10}", cond.Condition);
    }
}
