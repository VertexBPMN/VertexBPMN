using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Validation;

public class Phase3AdvancedValidationTransactionContextEndEventsTests
{
    // Cancel & terminate outside any transaction subprocess
    private const string OutsideTxXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:endEvent id="cancelEnd">
      <bpmn:cancelEventDefinition />
    </bpmn:endEvent>
    <bpmn:endEvent id="terminateEnd">
      <bpmn:terminateEventDefinition />
    </bpmn:endEvent>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="cancelEnd"/>
    <bpmn:sequenceFlow id="f2" sourceRef="cancelEnd" targetRef="terminateEnd"/>
  </bpmn:process>
</bpmn:definitions>
""";

    // Cancel & terminate inside a transaction subprocess (valid → no warnings)
    private const string InsideTxXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:subProcess id="tx1" triggeredByEvent="false" transaction="true">
      <bpmn:startEvent id="txStart"/>
      <bpmn:endEvent id="txCancelEnd">
        <bpmn:cancelEventDefinition />
      </bpmn:endEvent>
      <bpmn:endEvent id="txTerminateEnd">
        <bpmn:terminateEventDefinition />
      </bpmn:endEvent>
      <bpmn:sequenceFlow id="t1" sourceRef="txStart" targetRef="txCancelEnd"/>
      <bpmn:sequenceFlow id="t2" sourceRef="txCancelEnd" targetRef="txTerminateEnd"/>
    </bpmn:subProcess>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f0" sourceRef="start" targetRef="tx1"/>
    <bpmn:sequenceFlow id="f1" sourceRef="tx1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void TransactionRules_Disabled_NoStructuredDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(OutsideTxXml).GetAwaiter().GetResult();

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public void CancelAndTerminateOutsideTx_ProducesWarnings()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(OutsideTxXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Contains(model.ValidationDiagnostics!, d =>
            d.Code == "SEM-CANCEL-OUTSIDE-TX" &&
            d.ElementId == "cancelEnd" &&
            d.Severity == ValidationSeverity.Warning);

        Assert.Contains(model.ValidationDiagnostics!, d =>
            d.Code == "SEM-TERMINATE-OUTSIDE-TX" &&
            d.ElementId == "terminateEnd" &&
            d.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void CancelAndTerminateInsideTx_NoWarnings()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(InsideTxXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.Code is "SEM-CANCEL-OUTSIDE-TX" or "SEM-TERMINATE-OUTSIDE-TX");
    }
}