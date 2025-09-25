using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Validation;

public class Phase3AdvancedValidationEventBasedGatewayTests
{
    private const string InvalidTargetsXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:eventBasedGateway id="gw1"/>
    <!-- Invalid: userTask (not a catching event) -->
    <bpmn:userTask id="task1"/>
    <!-- Invalid: intermediateThrowEvent (throw, not catch) -->
    <bpmn:intermediateThrowEvent id="throwEvt">
      <bpmn:signalEventDefinition signalRef="sig1"/>
    </bpmn:intermediateThrowEvent>
    <!-- Valid: intermediateCatchEvent -->
    <bpmn:intermediateCatchEvent id="catchEvt">
      <bpmn:timerEventDefinition/>
    </bpmn:intermediateCatchEvent>
    <bpmn:endEvent id="end"/>

    <bpmn:sequenceFlow id="f0" sourceRef="start" targetRef="gw1"/>
    <bpmn:sequenceFlow id="f1" sourceRef="gw1" targetRef="task1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="gw1" targetRef="throwEvt"/>
    <bpmn:sequenceFlow id="f3" sourceRef="gw1" targetRef="catchEvt"/>
    <bpmn:sequenceFlow id="f4" sourceRef="catchEvt" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string AllValidXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:eventBasedGateway id="gw1"/>
    <bpmn:intermediateCatchEvent id="catch1">
      <bpmn:messageEventDefinition messageRef="m1"/>
    </bpmn:intermediateCatchEvent>
    <bpmn:intermediateCatchEvent id="catch2">
      <bpmn:signalEventDefinition signalRef="s1"/>
    </bpmn:intermediateCatchEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f0" sourceRef="start" targetRef="gw1"/>
    <bpmn:sequenceFlow id="f1" sourceRef="gw1" targetRef="catch1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="gw1" targetRef="catch2"/>
    <bpmn:sequenceFlow id="f3" sourceRef="catch1" targetRef="end"/>
    <bpmn:sequenceFlow id="f4" sourceRef="catch2" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void EventBasedGateway_Disabled_NoStructuredDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(InvalidTargetsXml).GetAwaiter().GetResult();

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public void EventBasedGateway_InvalidTargets_Reported()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(InvalidTargetsXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        var diags = model.ValidationDiagnostics!;

        // Two invalid outgoing targets: task1 (userTask), throwEvt (intermediateThrowEvent)
        Assert.Contains(diags, d =>
            d.Code == "SEM-EVENTGW-INVALID-OUTGOING" &&
            d.ElementId == "gw1" &&
            d.Message.Contains("task1"));

        Assert.Contains(diags, d =>
            d.Code == "SEM-EVENTGW-INVALID-OUTGOING" &&
            d.ElementId == "gw1" &&
            d.Message.Contains("throwEvt"));

        // Valid catch event target should not produce an error
        Assert.DoesNotContain(diags, d =>
            d.Code == "SEM-EVENTGW-INVALID-OUTGOING" &&
            d.Message.Contains("catchEvt"));
    }

    [Fact]
    public void EventBasedGateway_AllValid_NoDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(AllValidXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!,
            d => d.Code == "SEM-EVENTGW-INVALID-OUTGOING");
    }
}