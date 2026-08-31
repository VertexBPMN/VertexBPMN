using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationGlobalRefsTests
{
    private const string MissingGlobalsXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start">
      <bpmn:messageEventDefinition messageRef="msg1"/>
    </bpmn:startEvent>
    <bpmn:intermediateCatchEvent id="catch1">
      <bpmn:signalEventDefinition signalRef="sigX"/>
    </bpmn:intermediateCatchEvent>
    <bpmn:intermediateThrowEvent id="throwErr">
      <bpmn:errorEventDefinition errorRef="err404"/>
    </bpmn:intermediateThrowEvent>
    <bpmn:intermediateThrowEvent id="throwEsc">
      <bpmn:escalationEventDefinition escalationRef="esc99"/>
    </bpmn:intermediateThrowEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="catch1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="catch1" targetRef="throwErr"/>
    <bpmn:sequenceFlow id="f3" sourceRef="throwErr" targetRef="throwEsc"/>
    <bpmn:sequenceFlow id="f4" sourceRef="throwEsc" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string WithGlobalsXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:message id="msg1" name="M"/>
  <bpmn:signal id="sigX" name="S"/>
  <bpmn:error id="err404" name="E" errorCode="404"/>
  <bpmn:escalation id="esc99" name="ESC" escalationCode="E99"/>
  <bpmn:process id="p1">
    <bpmn:startEvent id="start">
      <bpmn:messageEventDefinition messageRef="msg1"/>
    </bpmn:startEvent>
    <bpmn:intermediateCatchEvent id="catch1">
      <bpmn:signalEventDefinition signalRef="sigX"/>
    </bpmn:intermediateCatchEvent>
    <bpmn:intermediateThrowEvent id="throwErr">
      <bpmn:errorEventDefinition errorRef="err404"/>
    </bpmn:intermediateThrowEvent>
    <bpmn:intermediateThrowEvent id="throwEsc">
      <bpmn:escalationEventDefinition escalationRef="esc99"/>
    </bpmn:intermediateThrowEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="catch1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="catch1" targetRef="throwErr"/>
    <bpmn:sequenceFlow id="f3" sourceRef="throwErr" targetRef="throwEsc"/>
    <bpmn:sequenceFlow id="f4" sourceRef="throwEsc" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task GlobalRefsMissing_Disabled_NoStructuredDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(MissingGlobalsXml, TestContext.Current.CancellationToken);

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public async Task GlobalRefsMissing_Enabled_ProducesAllFourDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(MissingGlobalsXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        var d = model.ValidationDiagnostics!;
        Assert.Contains(d, x => x.Code == "REF-GLOBAL-MESSAGE-MISSING" && x.ElementId == "start");
        Assert.Contains(d, x => x.Code == "REF-GLOBAL-SIGNAL-MISSING" && x.ElementId == "catch1");
        Assert.Contains(d, x => x.Code == "REF-GLOBAL-ERROR-MISSING" && x.ElementId == "throwErr");
        Assert.Contains(d, x => x.Code == "REF-GLOBAL-ESCALATION-MISSING" && x.ElementId == "throwEsc");
        // All severities should be Error
        Assert.All(d.Where(x => x.Code.StartsWith("REF-GLOBAL-", System.StringComparison.Ordinal)),
            x => Assert.Equal(ValidationSeverity.Error, x.Severity));
    }

    [Fact]
    public async Task GlobalRefsPresent_Enabled_NoMissingDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(WithGlobalsXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!,
            x => x.Code.StartsWith("REF-GLOBAL-", System.StringComparison.Ordinal));
    }
}