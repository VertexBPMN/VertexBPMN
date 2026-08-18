using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationBoundaryAttachedMissingTests
{
    private const string InvalidBoundaryXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:userTask id="task1"/>
    <!-- Invalid: attachedToRef points to non-existent 'ghostActivity' -->
    <bpmn:boundaryEvent id="bInvalid" attachedToRef="ghostActivity">
      <bpmn:timerEventDefinition />
    </bpmn:boundaryEvent>
    <!-- Valid boundary -->
    <bpmn:boundaryEvent id="bValid" attachedToRef="task1">
      <bpmn:timerEventDefinition />
    </bpmn:boundaryEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string NoBoundaryIssueXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="s"/>
    <bpmn:userTask id="task1"/>
    <bpmn:boundaryEvent id="bOk" attachedToRef="task1">
      <bpmn:timerEventDefinition />
    </bpmn:boundaryEvent>
    <bpmn:endEvent id="e"/>
    <bpmn:sequenceFlow id="f1" sourceRef="s" targetRef="task1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="task1" targetRef="e"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void BoundaryAttachedMissing_Disabled_NoStructuredDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(InvalidBoundaryXml).GetAwaiter().GetResult();

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public void BoundaryAttachedMissing_Enabled_ReportsError()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(InvalidBoundaryXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Contains(model.ValidationDiagnostics!, d =>
            d.Code == "REF-BOUNDARY-ATTACHED-MISSING" &&
            d.ElementId == "bInvalid" &&
            d.Severity == ValidationSeverity.Error);

        // Ensure valid boundary not flagged
        Assert.DoesNotContain(model.ValidationDiagnostics!, d =>
            d.ElementId == "bValid" && d.Code == "REF-BOUNDARY-ATTACHED-MISSING");
    }

    [Fact]
    public void BoundaryAttachedMissing_Enabled_NoIssues()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(NoBoundaryIssueXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.Code == "REF-BOUNDARY-ATTACHED-MISSING");
    }
}