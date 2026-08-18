using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationReachabilityTests
{
    private const string UnreachableNodeXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:userTask id="task1"/>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>

    <!-- Unreachable island -->
    <bpmn:userTask id="isolated"/>
    <bpmn:endEvent id="isolatedEnd"/>
    <bpmn:sequenceFlow id="f_isolated" sourceRef="isolated" targetRef="isolatedEnd"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string OrphanedEndOnlyXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:userTask id="task1"/>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>

    <!-- Orphaned end event with no incoming / not connected -->
    <bpmn:endEvent id="endOrphan"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void Reachability_Disabled_NoAdvisoryDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(UnreachableNodeXml).GetAwaiter().GetResult();

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public void Unreachable_Island_ProducesNodeAndFlowDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(UnreachableNodeXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        var diags = model.ValidationDiagnostics!;

        Assert.Contains(diags, d =>
            d.Code == "ADV-UNREACHABLE-NODE" &&
            d.ElementId == "isolated" &&
            d.Severity == ValidationSeverity.Info);

        Assert.Contains(diags, d =>
            d.Code == "ADV-UNREACHABLE-NODE" &&
            d.ElementId == "isolatedEnd" &&
            d.Severity == ValidationSeverity.Info);

        Assert.Contains(diags, d =>
            d.Code == "ADV-DEAD-SEQUENCE-FLOW" &&
            d.ElementId == "f_isolated" &&
            d.Severity == ValidationSeverity.Info);
    }

    [Fact]
    public void OrphanedEndEvent_ProducesOrphanedEndAndUnreachable()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(OrphanedEndOnlyXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        var diags = model.ValidationDiagnostics!;

        Assert.Contains(diags, d =>
            d.Code == "ADV-ORPHANED-END" &&
            d.ElementId == "endOrphan" &&
            d.Severity == ValidationSeverity.Info);

        // Also flagged as unreachable node
        Assert.Contains(diags, d =>
            d.Code == "ADV-UNREACHABLE-NODE" &&
            d.ElementId == "endOrphan");
    }

    [Fact]
    public void AllReachable_NoAdvisoryDiagnostics()
    {
        const string reachable = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:exclusiveGateway id="gw"/>
    <bpmn:userTask id="t1"/>
    <bpmn:userTask id="t2"/>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="gw"/>
    <bpmn:sequenceFlow id="f2" sourceRef="gw" targetRef="t1"/>
    <bpmn:sequenceFlow id="f3" sourceRef="gw" targetRef="t2"/>
    <bpmn:sequenceFlow id="f4" sourceRef="t1" targetRef="end"/>
    <bpmn:sequenceFlow id="f5" sourceRef="t2" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(reachable).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d =>
            d.Code is "ADV-UNREACHABLE-NODE" or "ADV-DEAD-SEQUENCE-FLOW" or "ADV-ORPHANED-END");
    }
}