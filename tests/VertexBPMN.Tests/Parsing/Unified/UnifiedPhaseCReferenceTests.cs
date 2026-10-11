using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Unified;

public class UnifiedPhaseCReferenceTests
{
	private readonly BpmnParser _parser = new();

	[Fact]
	public async Task Resolves_Message_And_Signal_References()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <message id='Msg_Order' name='OrderMessage'/>
  <signal id='Sig_Ready' name='ReadySignal'/>
  <process id='p1'>
    <startEvent id='start_msg'>
      <messageEventDefinition messageRef='Msg_Order'/>
    </startEvent>
    <intermediateThrowEvent id='throw_signal'>
      <signalEventDefinition signalRef='Sig_Ready'/>
    </intermediateThrowEvent>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Messages);
		Assert.Single(model.Messages);
		Assert.NotNull(model.Signals);
		Assert.Single(model.Signals);
		Assert.NotNull(model.Diagnostics);
		Assert.DoesNotContain(model.Diagnostics, d => d.Contains("messageRef", StringComparison.Ordinal));
		Assert.DoesNotContain(model.Diagnostics, d => d.Contains("signalRef", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Unknown_Refs_Produce_Diagnostics()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <startEvent id='start_msg'>
      <messageEventDefinition messageRef='MissingMsg'/>
    </startEvent>
    <intermediateThrowEvent id='throw_signal'>
      <signalEventDefinition signalRef='MissingSig'/>
    </intermediateThrowEvent>
    <endEvent id='end_error'>
      <errorEventDefinition errorRef='Err_404'/>
    </endEvent>
    <endEvent id='end_escalation'>
      <escalationEventDefinition escalationRef='Esc_1'/>
    </endEvent>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Diagnostics);
		Assert.Contains(model.Diagnostics, d => d.Contains("Unknown messageRef", StringComparison.Ordinal) && d.Contains("MissingMsg", StringComparison.Ordinal));
		Assert.Contains(model.Diagnostics, d => d.Contains("Unknown signalRef", StringComparison.Ordinal) && d.Contains("MissingSig", StringComparison.Ordinal));
		Assert.Contains(model.Diagnostics, d => d.Contains("Unknown errorRef", StringComparison.Ordinal) && d.Contains("Err_404", StringComparison.Ordinal));
		Assert.Contains(model.Diagnostics, d => d.Contains("Unknown escalationRef", StringComparison.Ordinal) && d.Contains("Esc_1", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Resolves_Error_And_Escalation()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <error id='Err_1' name='Boom' errorCode='E1'/>
  <escalation id='Esc_1' name='Escalate' escalationCode='ES1'/>
  <process id='p1'>
    <endEvent id='end_error'>
      <errorEventDefinition errorRef='Err_1'/>
    </endEvent>
    <endEvent id='end_escalation'>
      <escalationEventDefinition escalationRef='Esc_1'/>
    </endEvent>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Errors);
		Assert.Single(model.Errors);
		Assert.NotNull(model.Escalations);
		Assert.Single(model.Escalations);
		Assert.NotNull(model.Diagnostics);
		Assert.DoesNotContain(model.Diagnostics, d => d.Contains("errorRef", StringComparison.Ordinal));
		Assert.DoesNotContain(model.Diagnostics, d => d.Contains("escalationRef", StringComparison.Ordinal));
	}
}
