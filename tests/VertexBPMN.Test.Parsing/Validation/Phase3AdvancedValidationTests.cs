using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Validation;

public class Phase3AdvancedValidationTests
{
    private const string DuplicateIdXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="s1"/>
    <bpmn:userTask id="dup"/>
    <bpmn:serviceTask id="dup"/>
    <bpmn:endEvent id="e1"/>
    <bpmn:sequenceFlow id="f1" sourceRef="s1" targetRef="dup"/>
    <bpmn:sequenceFlow id="f2" sourceRef="dup" targetRef="e1"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void AdvancedValidation_Disabled_NoStructuredDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(DuplicateIdXml).GetAwaiter().GetResult();

        Assert.Null(model.ValidationDiagnostics);
        Assert.Contains(model.Diagnostics, m => m.StartsWith("Duplicate ID:"));
    }

    [Fact]
    public void AdvancedValidation_Enabled_DuplicateIdReportedStructurally()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(DuplicateIdXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        var diag = Assert.Single(model.ValidationDiagnostics!);
        Assert.Equal("STR-DUP-ID", diag.Code);
        Assert.Equal("dup", diag.ElementId);
        Assert.Equal(ValidationSeverity.Error, diag.Severity);
        // Legacy message still present
        Assert.Contains(model.Diagnostics, m => m.StartsWith("Duplicate ID:"));
    }

    [Fact]
    public void AdvancedValidation_Enabled_NoDuplicates_NoStructuredDiagnostics()
    {
        const string cleanXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="s1"/>
    <bpmn:endEvent id="e1"/>
    <bpmn:sequenceFlow id="f1" sourceRef="s1" targetRef="e1"/>
  </bpmn:process>
</bpmn:definitions>
""";
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(cleanXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Empty(model.ValidationDiagnostics!);
    }
}