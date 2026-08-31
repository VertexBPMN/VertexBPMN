using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationMultiInstanceConflictTests
{
    private const string ConflictXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <!-- Conflict: loopCardinality AND camunda:collection -->
    <bpmn:serviceTask id="miConflict">
      <bpmn:multiInstanceLoopCharacteristics camunda:collection="items">
        <bpmn:loopCardinality>5</bpmn:loopCardinality>
      </bpmn:multiInstanceLoopCharacteristics>
    </bpmn:serviceTask>

    <!-- Valid MI: only collection -->
    <bpmn:serviceTask id="miOk1">
      <bpmn:multiInstanceLoopCharacteristics camunda:collection="things"/>
    </bpmn:serviceTask>

    <!-- Valid MI: only loopCardinality -->
    <bpmn:serviceTask id="miOk2">
      <bpmn:multiInstanceLoopCharacteristics>
        <bpmn:loopCardinality>3</bpmn:loopCardinality>
      </bpmn:multiInstanceLoopCharacteristics>
    </bpmn:serviceTask>

    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="miConflict"/>
    <bpmn:sequenceFlow id="f2" sourceRef="miConflict" targetRef="miOk1"/>
    <bpmn:sequenceFlow id="f3" sourceRef="miOk1" targetRef="miOk2"/>
    <bpmn:sequenceFlow id="f4" sourceRef="miOk2" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string CleanXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="s"/>
    <bpmn:endEvent id="e"/>
    <bpmn:sequenceFlow id="f" sourceRef="s" targetRef="e"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task MultiInstanceConflict_Disabled_NoStructuredDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(ConflictXml, TestContext.Current.CancellationToken);

        Assert.Null(model.ValidationDiagnostics);
    }

    //[Fact]
    //public void MultiInstanceConflict_Enabled_ReportsWarning()
    //{
    //    var model = await new BpmnParser(new BpmnParserOptions
    //    {
    //        RoundtripMode = BpmnRoundtripMode.Strict,
    //        EnableAdvancedValidation = true
    //    }).ParseAsync(ConflictXml);

    //    Assert.NotNull(model.ValidationDiagnostics);
    //    Assert.Contains(model.ValidationDiagnostics!, d =>
    //        d.Code == "SEM-MI-CONFLICT" &&
    //        d.ElementId == "miConflict" &&
    //        d.Severity == ValidationSeverity.Warning);
    //    // Ensure no false positives
    //    Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.ElementId == "miOk1");
    //    Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.ElementId == "miOk2");
    //}

    [Fact]
    public async Task MultiInstanceConflict_Enabled_NoConflicts_EmptyForRule()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(CleanXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.Code == "SEM-MI-CONFLICT");
    }
}