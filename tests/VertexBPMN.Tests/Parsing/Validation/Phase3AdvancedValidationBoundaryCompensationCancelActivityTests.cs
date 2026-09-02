using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationBoundaryCompensationCancelActivityTests
{
    private const string InvalidCompBoundaryXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:userTask id="taskA"/>
    <!-- Missing cancelActivity (defaults to true) -> violation -->
    <bpmn:boundaryEvent id="bc1" attachedToRef="taskA">
      <bpmn:compensateEventDefinition />
    </bpmn:boundaryEvent>
    <!-- Explicit cancelActivity='true' -> violation -->
    <bpmn:boundaryEvent id="bc2" attachedToRef="taskA" cancelActivity="true">
      <bpmn:compensateEventDefinition />
    </bpmn:boundaryEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="taskA" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string ValidCompBoundaryXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:userTask id="taskA"/>
    <!-- Proper non-interrupting compensation boundary -->
    <bpmn:boundaryEvent id="bcOk" attachedToRef="taskA" cancelActivity="false">
      <bpmn:compensateEventDefinition />
    </bpmn:boundaryEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="taskA" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task BoundaryCompensationCancelActivity_Disabled_NoStructuredDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(InvalidCompBoundaryXml, TestContext.Current.CancellationToken);

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public async Task BoundaryCompensationCancelActivity_Enabled_ReportsErrors()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(InvalidCompBoundaryXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        var diags = model.ValidationDiagnostics!;
        Assert.Contains(diags, d =>
            d.Code == "SEM-BOUNDARY-COMPENSATION-CANCELACTIVITY" &&
            d.ElementId == "bc1" &&
            d.Severity == ValidationSeverity.Error);

        Assert.Contains(diags, d =>
            d.Code == "SEM-BOUNDARY-COMPENSATION-CANCELACTIVITY" &&
            d.ElementId == "bc2" &&
            d.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task BoundaryCompensationCancelActivity_Enabled_NoViolationWhenFalse()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(ValidCompBoundaryXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!,
            d => d.Code == "SEM-BOUNDARY-COMPENSATION-CANCELACTIVITY");
    }
}