using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Validation;

public class Phase3AdvancedValidationDataObjectsAndAssociationsTests
{
    private const string MissingRefsXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:endEvent id="end"/>
    <bpmn:dataObject id="do1" name="DataOne"/>
    <!-- dataObjectReference with unknown dataObjectRef -->
    <bpmn:dataObjectReference id="dorMissing" dataObjectRef="doX"/>
    <!-- valid reference -->
    <bpmn:dataObjectReference id="dorOk" dataObjectRef="do1"/>
    <!-- association with missing source and target -->
    <bpmn:association id="assoc1" sourceRef="ghostSource" targetRef="ghostTarget"/>
    <!-- association with one valid, one invalid -->
    <bpmn:association id="assoc2" sourceRef="start" targetRef="ghostTarget2"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string ValidRefsXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:endEvent id="end"/>
    <bpmn:dataObject id="do1" name="DataOne"/>
    <bpmn:dataObjectReference id="dorOk" dataObjectRef="do1"/>
    <bpmn:association id="assoc1" sourceRef="start" targetRef="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void DataObjectAndAssociationRules_Disabled_NoStructuredDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(MissingRefsXml).GetAwaiter().GetResult();

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public void MissingReferences_Reported()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(MissingRefsXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        var diags = model.ValidationDiagnostics!;

        // DataObjectReference target missing
        Assert.Contains(diags, d =>
            d.Code == "REF-DATAOBJECTREF-TARGET-MISSING" &&
            d.ElementId == "dorMissing" &&
            d.Severity == ValidationSeverity.Error);

        // Association endpoint missing (both endpoints for assoc1)
        Assert.Contains(diags, d =>
            d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" &&
            d.ElementId == "assoc1" &&
            d.Message.Contains("sourceRef", System.StringComparison.OrdinalIgnoreCase));

        Assert.Contains(diags, d =>
            d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" &&
            d.ElementId == "assoc1" &&
            d.Message.Contains("targetRef", System.StringComparison.OrdinalIgnoreCase));

        // Association second (only target missing)
        Assert.DoesNotContain(diags, d =>
            d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" &&
            d.ElementId == "assoc2" &&
            d.Message.Contains("sourceRef", System.StringComparison.OrdinalIgnoreCase)); // source is valid

        Assert.Contains(diags, d =>
            d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" &&
            d.ElementId == "assoc2" &&
            d.Message.Contains("targetRef", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AllValid_NoDiagnosticsForTheseRules()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(ValidRefsXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d =>
            d.Code is "REF-DATAOBJECTREF-TARGET-MISSING" or "REF-ASSOCIATION-ENDPOINT-MISSING");
    }
}