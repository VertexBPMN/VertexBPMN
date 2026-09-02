using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Unified;

public class UnifiedPhaseCReferenceTests
{
    private readonly BpmnParser _parser = new();

    [Fact]
    public async Task Resolves_Message_And_Signal_References()
    {
        var xml = """
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
        Assert.Single(model.Messages);
        Assert.Single(model.Signals);
        Assert.DoesNotContain(model.Diagnostics, d => d.Contains("messageRef"));
        Assert.DoesNotContain(model.Diagnostics, d => d.Contains("signalRef"));
    }

    [Fact]
    public async Task Unknown_Refs_Produce_Diagnostics()
    {
        var xml = """
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
        var messageRefs = model.Diagnostics.Where(se => se.Contains("Unknown messageRef") && se.Contains("MissingMsg")).ToList();
        Assert.Contains(model.Diagnostics, d => d.Contains("Unknown messageRef") && d.Contains("MissingMsg"));
        Assert.Contains(model.Diagnostics, d => d.Contains("Unknown signalRef") && d.Contains("MissingSig"));
        Assert.Contains(model.Diagnostics, d => d.Contains("Unknown errorRef") && d.Contains("Err_404"));
        Assert.Contains(model.Diagnostics, d => d.Contains("Unknown escalationRef") && d.Contains("Esc_1"));
    }

    [Fact]
    public async Task Resolves_Error_And_Escalation()
    {
        var xml = """
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
        Assert.Single(model.Errors);
        Assert.Single(model.Escalations);
        Assert.DoesNotContain(model.Diagnostics, d => d.Contains("errorRef"));
        Assert.DoesNotContain(model.Diagnostics, d => d.Contains("escalationRef"));
    }
}
