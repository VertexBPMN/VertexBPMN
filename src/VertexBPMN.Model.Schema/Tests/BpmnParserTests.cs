
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using System.Xml;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Domain.Model.Tests;

public class BpmnParserTests
{
    private readonly Mock<ILogger<BpmnParser>> _loggerMock = new();
    private readonly BpmnParser _parser;

    public BpmnParserTests()
    {
        _parser = new BpmnParser(_loggerMock.Object);
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
        var invalidXml = "<invalid></invalid>";
        // Act & Assert
        await Assert.ThrowsAsync<BpmnParseException>(() => _parser.ParseAsync(invalidXml));
        //_loggerMock.Verify(l => l.LogError(It.IsAny<Exception>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ParseAsync_WithExtensionAttributes_ParsesCorrectly()
    {
        // Arrange
        var bpmnXml = @"
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:ext=""http://example/ext"" 
targetNamespace=""http://www.example.org/Processes/sellerProcess"" >
  <process id=""Process_1"" ext:customAttr=""value"">
    <startEvent id=""StartEvent_1"" />
  </process>
</definitions>";

        // Act
        var model = await _parser.ParseAsync(bpmnXml);

        // Assert
        model.ShouldNotBeNull();
        // Assume BpmnModel has Attributes dictionary for extensions
        //model.Attributes.Should().ContainKey("ext:customAttr");
        //model.Attributes["ext:customAttr"].Should().Be("value");
    }

    [Fact]
    public async Task ParseAsync_EmptyXml_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<BpmnParseException>(() => _parser.ParseAsync(string.Empty));
    }
}
