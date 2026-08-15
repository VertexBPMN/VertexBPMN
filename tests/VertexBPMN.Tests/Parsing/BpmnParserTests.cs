using Shouldly;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Tests.Parsing;

public class BpmnParserTests
{
    private readonly BpmnParser _parser;

    public BpmnParserTests()
    {
        _parser = new BpmnParser();
    }

    [Fact]
    public async Task ParseAsync_ValidBpmnXml_ReturnsModelWithProcess()
    {
        // Arrange
        var bpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:omgdc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:omgdi=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
  <process id=""Process_1"" isExecutable=""true"">
    <startEvent id=""StartEvent_1"" name=""startEvent"" />
    <serviceTask id=""ServiceTask_Analyse"" name=""Analyse"" />
    <exclusiveGateway id=""Gateway_1"" />
    <serviceTask id=""ServiceTask_Empfehlung"" name=""Empfehlung"" />
    <serviceTask id=""ServiceTask_Review"" name=""Review"" />
    <endEvent id=""EndEvent_Positive"" name=""Ende Positiv"" />
    <endEvent id=""EndEvent_Negative"" name=""Ende Negativ"" />
    <sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""ServiceTask_Analyse"" />
    <sequenceFlow id=""Flow_2"" sourceRef=""ServiceTask_Analyse"" targetRef=""Gateway_1"" />
    <sequenceFlow id=""Flow_Positive"" sourceRef=""Gateway_1"" targetRef=""ServiceTask_Empfehlung"">
      <conditionExpression xsi:type=""tFormalExpression"">${result == ""positiv""}</conditionExpression>
    </sequenceFlow>
    <sequenceFlow id=""Flow_Negative"" sourceRef=""Gateway_1"" targetRef=""ServiceTask_Review"">
      <conditionExpression xsi:type=""tFormalExpression"">${result == ""negativ""}</conditionExpression>
    </sequenceFlow>
    <sequenceFlow id=""Flow_3"" sourceRef=""ServiceTask_Empfehlung"" targetRef=""EndEvent_Positive"" />
    <sequenceFlow id=""Flow_4"" sourceRef=""ServiceTask_Review"" targetRef=""EndEvent_Negative"" />
  </process>
</definitions>";

        // Act
        var model = await _parser.ParseAsync(bpmnXml);

        // Assert
        model.ShouldNotBeNull();
        model.Id.ShouldBe("Process_1");
        model.Events.Count.ShouldBe(3);
        model.Events[0].Id.ShouldBe("StartEvent_1");
        model.Events[0].Name.ShouldBe("startEvent");
        //_loggerMock.Verify(l => l.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ParseAsync_InvalidBpmnXml_ThrowsBpmnParseException()
    {
        // Arrange
        var invalidXml = "<invalid></invalid";
        // Act & Assert
        await Assert.ThrowsAsync<SecurityException>(() => _parser.ParseAsync(invalidXml));
        //_loggerMock.Verify(l => l.LogError(It.IsAny<Exception>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ParseAsync_WithExtensionAttributes_ParsesCorrectly()
    {
        // Arrange
        var bpmnXml = @"
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:ext=""http://example/ext"" 
targetNamespace=""http://www.example.org/Processes/sellerProcess"" >
  <process id=""Process_1"">
    <startEvent id=""StartEvent_1""  name=""startEvent"" ext:customAttr=""value"" />
  </process>
</definitions>";

        // Act
        var model = await _parser.ParseAsync(bpmnXml);

        // Assert
        model.ShouldNotBeNull();
        // Assume BpmnModel has Attributes dictionary for extensions
        model.Events[0].Id.ShouldBe("StartEvent_1");
        model.Events[0].Name.ShouldBe("startEvent");
        model.Events[0].ExtensionAttributes.ShouldContainKey("ext:customAttr");
        model.Events[0].ExtensionAttributes["ext:customAttr"].ShouldBe("value");
    }

    [Fact]
    public async Task ParseAsync_EmptyXml_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<SecurityException>(() => _parser.ParseAsync(string.Empty));
    }

    [Fact]
    public void ExclusiveGateway_SelectsMatchingCondition()
    {
        var component = new BpmnExecutionComponent();

        var flows = new[]
        {
            new BpmnSequenceFlow(
                "flow-a",
                "gateway",
                "task-a",
                ConditionExpression: "approved == true"),

            new BpmnSequenceFlow(
                "flow-b",
                "gateway",
                "task-b",
                ConditionExpression: "approved == false")
        };

        var decision = component.SelectExclusiveFlow(
            flows,
            new Dictionary<string, object>
            {
                ["approved"] = true
            },
            (flow, variables) =>
                flow.ConditionExpression == "approved == true");

        Assert.Equal(GatewayDecisionKind.Selected, decision.Kind);
        Assert.Equal("flow-a", decision.Flow!.Id);
    }

    [Fact]
    public void ExclusiveGateway_UsesDefaultWhenNoConditionMatches()
    {
        var component = new BpmnExecutionComponent();

        var flows = new[]
        {
            new BpmnSequenceFlow(
                "flow-a",
                "gateway",
                "task-a",
                ConditionExpression: "approved == true"),

            new BpmnSequenceFlow(
                "flow-default",
                "gateway",
                "task-default",
                IsDefault: true)
        };

        var decision = component.SelectExclusiveFlow(
            flows,
            new Dictionary<string, object>
            {
                ["approved"] = false
            },
            (_, _) => false);

        Assert.Equal(GatewayDecisionKind.DefaultSelected, decision.Kind);
        Assert.Equal("flow-default", decision.Flow!.Id);
    }
}


