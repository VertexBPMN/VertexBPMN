using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Validation;

public class Phase3AdvancedValidationEventSubprocessStartTypeTests
{
    private const string InvalidStartTypesXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <!-- Event subprocess with various invalid start events -->
    <bpmn:subProcess id="esp1" triggeredByEvent="true">
      <!-- No event definition (invalid) -->
      <bpmn:startEvent id="startNone"/>
      <!-- Terminate event definition (invalid for event subprocess start) -->
      <bpmn:startEvent id="startTerminate">
        <bpmn:terminateEventDefinition />
      </bpmn:startEvent>
      <!-- Link event definition (invalid) -->
      <bpmn:startEvent id="startLink">
        <bpmn:linkEventDefinition name="L1"/>
      </bpmn:startEvent>
      <!-- Valid: timer -->
      <bpmn:startEvent id="startTimer">
        <bpmn:timerEventDefinition/>
      </bpmn:startEvent>
    </bpmn:subProcess>

    <!-- Normal subprocess (not triggeredByEvent) — its plain start (no def) is OK -->
    <bpmn:subProcess id="spNormal">
      <bpmn:startEvent id="normalStart"/>
    </bpmn:subProcess>

    <bpmn:startEvent id="rootStart"/>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="rootStart" targetRef="esp1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="esp1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string AllValidXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:subProcess id="esp1" triggeredByEvent="true">
      <bpmn:startEvent id="msgStart">
        <bpmn:messageEventDefinition messageRef="m1"/>
      </bpmn:startEvent>
      <bpmn:startEvent id="sigStart">
        <bpmn:signalEventDefinition signalRef="s1"/>
      </bpmn:startEvent>
      <bpmn:startEvent id="errStart">
        <bpmn:errorEventDefinition errorRef="e1"/>
      </bpmn:startEvent>
      <bpmn:startEvent id="escStart">
        <bpmn:escalationEventDefinition escalationRef="esc1"/>
      </bpmn:startEvent>
      <bpmn:startEvent id="condStart">
        <bpmn:conditionalEventDefinition>
          <bpmn:conditionExpression>${x>1}</bpmn:conditionExpression>
        </bpmn:conditionalEventDefinition>
      </bpmn:startEvent>
      <bpmn:startEvent id="compStart">
        <bpmn:compensateEventDefinition />
      </bpmn:startEvent>
      <bpmn:startEvent id="timerStart">
        <bpmn:timerEventDefinition/>
      </bpmn:startEvent>
    </bpmn:subProcess>
    <bpmn:startEvent id="rootStart"/>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="rootStart" targetRef="esp1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="esp1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void EventSubprocessStartType_Disabled_NoStructuredDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(InvalidStartTypesXml).GetAwaiter().GetResult();

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public void EventSubprocessStartType_InvalidReported()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(InvalidStartTypesXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        var diags = model.ValidationDiagnostics!;

        // Expect separate diagnostics for each invalid start inside event subprocess
        Assert.Contains(diags, d => d.Code == "SEM-EVENTSUBPROCESS-START-TYPE" && d.ElementId == "startNone"      && d.Message.Contains("none"));
        Assert.Contains(diags, d => d.Code == "SEM-EVENTSUBPROCESS-START-TYPE" && d.ElementId == "startTerminate" && d.Message.Contains("terminate"));
        Assert.Contains(diags, d => d.Code == "SEM-EVENTSUBPROCESS-START-TYPE" && d.ElementId == "startLink"      && d.Message.Contains("link"));

        // Valid timer not flagged
        Assert.DoesNotContain(diags, d => d.Code == "SEM-EVENTSUBPROCESS-START-TYPE" && d.ElementId == "startTimer");
        // Normal (non event) subprocess start not flagged
        Assert.DoesNotContain(diags, d => d.ElementId == "normalStart" && d.Code == "SEM-EVENTSUBPROCESS-START-TYPE");
    }

    [Fact]
    public void EventSubprocessStartType_AllValid_NoDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(AllValidXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!,
            d => d.Code == "SEM-EVENTSUBPROCESS-START-TYPE");
    }
}