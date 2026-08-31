using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationLaneFlowNodeRefsTests
{
    private const string MissingRefXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:laneSet id="ls1">
      <bpmn:lane id="lane1">
        <bpmn:flowNodeRef>taskDoesNotExist</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id="start"/>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f" sourceRef="start" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string ValidRefsXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:endEvent id="end"/>
    <bpmn:laneSet id="ls1">
      <bpmn:lane id="lane1">
        <bpmn:flowNodeRef>start</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>end</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:sequenceFlow id="f" sourceRef="start" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task LaneFlowNodeRefs_Disabled_NoStructuredDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(MissingRefXml, TestContext.Current.CancellationToken);

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public async Task LaneFlowNodeRefs_MissingReference_ProducesWarning()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(MissingRefXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Contains(model.ValidationDiagnostics!, d =>
            d.Code == "REF-LANE-FLOWNODE-MISSING" &&
            d.ElementId == "lane1" &&
            d.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public async Task LaneFlowNodeRefs_AllValid_NoDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(ValidRefsXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.Code == "REF-LANE-FLOWNODE-MISSING");
    }
}