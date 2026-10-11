using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing;

public class EventDefinitionParsingTests
{
	private readonly BpmnParser _parser = new();

	[Fact]
	public async Task Parses_Timer_Event_Definitions()
	{
		const string xml = """
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
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Events);
		var ev = Assert.Single(model.Events);
		Assert.NotNull(ev.Definitions);
		var def = Assert.Single(ev.Definitions);
		Assert.Equal("timer", def.Kind);
		Assert.Equal("PT5M", ((TimerEventDefinition)def).TimeDuration);
	}

	[Fact]
	public async Task Parses_Message_Start_Event()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <startEvent id='start_msg'>
      <messageEventDefinition messageRef='Msg_A' />
    </startEvent>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Events);
		var ev = Assert.Single(model.Events);
		Assert.NotNull(ev.Definitions);
		var def = Assert.Single(ev.Definitions);
		var msg = Assert.IsType<MessageEventDefinition>(def);
		Assert.Equal("Msg_A", msg.MessageRef);
	}

	[Fact]
	public async Task Parses_Signal_And_Error_End_Events()
	{
		const string xml = """
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
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Events);
		Assert.Equal(2, model.Events.Count);
		var signalDefinitions = model.Events[0].Definitions;
		Assert.NotNull(signalDefinitions);
		var signal = Assert.IsType<SignalEventDefinition>(signalDefinitions[0]);
		Assert.Equal("Sig1", signal.SignalRef);
		var errorDefinitions = model.Events[1].Definitions;
		Assert.NotNull(errorDefinitions);
		var error = Assert.IsType<ErrorEventDefinition>(errorDefinitions[0]);
		Assert.Equal("Err42", error.ErrorRef);
	}

	[Fact]
	public async Task Parses_Conditional_Intermediate()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <intermediateCatchEvent id='cond1'>
      <conditionalEventDefinition>
        <condition><![CDATA[${x > 10}]]></condition>
      </conditionalEventDefinition>
    </intermediateCatchEvent>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Events);
		var ev = Assert.Single(model.Events);
		Assert.NotNull(ev.Definitions);
		var cond = Assert.IsType<ConditionalEventDefinition>(ev.Definitions[0]);
		Assert.Equal("${x > 10}", cond.Condition);
	}
}
