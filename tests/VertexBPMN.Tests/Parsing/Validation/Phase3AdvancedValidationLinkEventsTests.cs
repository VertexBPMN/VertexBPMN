using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationLinkEventsTests
{
    private const string UnmatchedLinkXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:intermediateThrowEvent id="throw1">
      <bpmn:linkEventDefinition name="L1"/>
    </bpmn:intermediateThrowEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="throw1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="throw1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string MultipleThrowLinkXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <!-- Two throw link events with same name (invalid) -->
    <bpmn:intermediateThrowEvent id="throwA">
      <bpmn:linkEventDefinition name="L_MULTI"/>
    </bpmn:intermediateThrowEvent>
    <bpmn:intermediateThrowEvent id="throwB">
      <bpmn:linkEventDefinition name="L_MULTI"/>
    </bpmn:intermediateThrowEvent>
    <!-- Matching catch link event (still multiple throws remain invalid) -->
    <bpmn:intermediateCatchEvent id="catch1">
      <bpmn:linkEventDefinition name="L_MULTI"/>
    </bpmn:intermediateCatchEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="throwA"/>
    <bpmn:sequenceFlow id="f2" sourceRef="throwA" targetRef="throwB"/>
    <bpmn:sequenceFlow id="f3" sourceRef="throwB" targetRef="catch1"/>
    <bpmn:sequenceFlow id="f4" sourceRef="catch1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string ValidLinkXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:intermediateThrowEvent id="throw1">
      <bpmn:linkEventDefinition name="LINK_OK"/>
    </bpmn:intermediateThrowEvent>
    <bpmn:intermediateCatchEvent id="catch1">
      <bpmn:linkEventDefinition name="LINK_OK"/>
    </bpmn:intermediateCatchEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="throw1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="throw1" targetRef="catch1"/>
    <bpmn:sequenceFlow id="f3" sourceRef="catch1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task LinkEvents_Disabled_NoStructuredDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(UnmatchedLinkXml, TestContext.Current.CancellationToken);

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public async Task LinkEvents_Unmatched_ProducesSemanticError()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(UnmatchedLinkXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Contains(model.ValidationDiagnostics!, d =>
            d.Code == "SEM-LINK-UNMATCHED" &&
            d.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task LinkEvents_MultipleThrow_ProducesSemanticError()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(MultipleThrowLinkXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        // Multiple throw rule
        Assert.Contains(model.ValidationDiagnostics!, d =>
            d.Code == "SEM-LINK-MULTIPLE-THROW" &&
            d.Severity == ValidationSeverity.Error);
        // No unmatched error for L_MULTI (it is caught)
        Assert.DoesNotContain(model.ValidationDiagnostics!, d =>
            d.Code == "SEM-LINK-UNMATCHED" && d.Message.Contains("L_MULTI"));
    }

    [Fact]
    public async Task LinkEvents_Valid_NoSemanticLinkDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(ValidLinkXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d =>
            d.Code is "SEM-LINK-UNMATCHED" or "SEM-LINK-MULTIPLE-THROW");
    }
}