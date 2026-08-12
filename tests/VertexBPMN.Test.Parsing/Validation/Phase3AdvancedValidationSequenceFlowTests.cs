using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Validation;

public class Phase3AdvancedValidationSequenceFlowTests
{
    private const string InvalidEndpointsXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="s1"/>
    <!-- flow f1 has invalid targetRef -->
    <bpmn:sequenceFlow id="f1" sourceRef="s1" targetRef="missingTask"/>
    <!-- flow f2 has invalid sourceRef AND targetRef -->
    <bpmn:sequenceFlow id="f2" sourceRef="ghost" targetRef="none"/>
    <bpmn:endEvent id="e1"/>
    <!-- valid flow -->
    <bpmn:sequenceFlow id="f3" sourceRef="s1" targetRef="e1"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void SequenceFlowInvalidEndpoints_Disabled_NoStructuredDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(InvalidEndpointsXml).GetAwaiter().GetResult();

        Assert.Null(model.ValidationDiagnostics); // feature off
    }

    [Fact]
    public void SequenceFlowInvalidEndpoints_Enabled_ProducesDiagnostics()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(InvalidEndpointsXml).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        var diags = model.ValidationDiagnostics!;
        // Expect 3 errors: f1 target, f2 source, f2 target
        Assert.Equal(4, diags.Count);

        Assert.Contains(diags, d =>
            d.Code == "REF-SEQUENCE-ENDPOINT" &&
            d.ElementId == "f1" &&
            d.Message.Contains("targetRef 'missingTask'"));

        Assert.Contains(diags, d =>
            d.Code == "REF-SEQUENCE-ENDPOINT" &&
            d.ElementId == "f2" &&
            d.Message.Contains("sourceRef 'ghost'"));

        Assert.Contains(diags, d =>
            d.Code == "REF-SEQUENCE-ENDPOINT" &&
            d.ElementId == "f2" &&
            d.Message.Contains("targetRef 'none'"));
    }

    [Fact]
    public void SequenceFlowValid_NoEndpointDiagnostics()
    {
        const string valid = """
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
        }).ParseAsync(valid).GetAwaiter().GetResult();

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.Code == "REF-SEQUENCE-ENDPOINT");
    }
}