using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationDefaultFlowConditionTests
{
    private const string XmlWithDefaultCondition = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:exclusiveGateway id="gw1" default="flow_default"/>
    <bpmn:userTask id="task1"/>
    <bpmn:userTask id="task2"/>
    <!-- Violating default flow (has condition) -->
    <bpmn:sequenceFlow id="flow_default" sourceRef="gw1" targetRef="task1">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression">${x > 5}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <!-- Non-default flow (may legitimately have condition) -->
    <bpmn:sequenceFlow id="flow_other" sourceRef="gw1" targetRef="task2">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression">${y == 1}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="fEnd1" sourceRef="task1" targetRef="end"/>
    <bpmn:sequenceFlow id="fEnd2" sourceRef="task2" targetRef="end"/>
    <bpmn:endEvent id="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private const string XmlWithoutDefaultCondition = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:exclusiveGateway id="gw1" default="flow_default"/>
    <bpmn:userTask id="task1"/>
    <bpmn:userTask id="task2"/>
    <!-- Default flow WITHOUT condition (valid) -->
    <bpmn:sequenceFlow id="flow_default" sourceRef="gw1" targetRef="task1"/>
    <!-- Conditional non-default flow (valid) -->
    <bpmn:sequenceFlow id="flow_other" sourceRef="gw1" targetRef="task2">
      <bpmn:conditionExpression>${cond}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="fEnd1" sourceRef="task1" targetRef="end"/>
    <bpmn:sequenceFlow id="fEnd2" sourceRef="task2" targetRef="end"/>
    <bpmn:endEvent id="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task DefaultFlowCondition_Disabled_NoStructuredDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(XmlWithDefaultCondition, TestContext.Current.CancellationToken);

        Assert.Null(model.ValidationDiagnostics);
    }

    [Fact]
    public async Task DefaultFlowCondition_Enabled_FindsViolation()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(XmlWithDefaultCondition, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Contains(model.ValidationDiagnostics!, d =>
            d.Code == "SEM-DEFAULT-WITH-CONDITION" &&
            d.ElementId == "flow_default" &&
            d.Severity == ValidationSeverity.Error);
        // Ensure non-default conditional flow not flagged
        Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.ElementId == "flow_other" && d.Code == "SEM-DEFAULT-WITH-CONDITION");
    }

    [Fact]
    public async Task DefaultFlowCondition_Enabled_NoViolationWhenValid()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(XmlWithoutDefaultCondition, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.DoesNotContain(model.ValidationDiagnostics!, d => d.Code == "SEM-DEFAULT-WITH-CONDITION");
    }
}