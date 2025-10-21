
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using System.Xml;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn.Exceptions;
using Xunit;

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
        var bpmnXml = @"
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" 
targetNamespace=""http://www.example.org/Processes/sellerProcess"" >
  <process id=""Process_1"">
    <startEvent id=""StartEvent_1"" />
  </process>
</definitions>";

        // Act
        var model = await _parser.ParseAsync(bpmnXml);

        // Assert
        model.ShouldNotBeNull();
        model.Id.ShouldBe("Process_1");
        model.Events.Count.ShouldBe(1);
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

//    [Fact]
//    public async Task ParseAsync_WithExtensionAttributes_ParsesCorrectly()
//    {
//        // Arrange
//        var bpmnXml = @"
//<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:ext=""http://example/ext"" 
//targetNamespace=""http://www.example.org/Processes/sellerProcess"" >
//  <process id=""Process_1"" ext:customAttr=""value"">
//    <startEvent id=""StartEvent_1"" />
//  </process>
//</definitions>";

//        // Act
//        var model = await _parser.ParseAsync(bpmnXml);

//        // Assert
//        model.ShouldNotBeNull();
//        // Assume BpmnModel has Attributes dictionary for extensions
//       //model.Attributes.Should().ContainKey("ext:customAttr");
//       //model.Attributes["ext:customAttr"].Should().Be("value");
//    }

    [Fact]
    public async Task ParseAsync_EmptyXml_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<BpmnParseException>(() => _parser.ParseAsync(string.Empty));
    }
}
