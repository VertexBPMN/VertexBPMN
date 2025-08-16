using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Core.Handlers;
using Xunit;
namespace VertexBPMN.Tests.Tasks;
public class CancelApplicationServiceTaskHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCancelApplicationSuccessfully()
    {
        // Arrange
        List<LogLevel> logLevels = new List<LogLevel>();
        List<string> logMessages = new List<string>();
        var loggerMock = new Mock<ILogger<CancelApplicationServiceTaskHandler>>();

        var handler = new CancelApplicationServiceTaskHandler(loggerMock.Object);
        var variables = new Dictionary<string, object>
        {
            { "applicationId", "12345" },
            { "reason", "Customer request" }
        };

        // Act
        await handler.ExecuteAsync(new Dictionary<string, string>(), variables);

        // Assert
        Assert.True(variables.ContainsKey("applicationStatus"));
        Assert.Equal("Cancelled", variables["applicationStatus"]);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("12345")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenApplicationIdIsMissing()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CancelApplicationServiceTaskHandler>>();
        var handler = new CancelApplicationServiceTaskHandler(loggerMock.Object);
        var variables = new Dictionary<string, object>
        {
            { "reason", "Customer request" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(new Dictionary<string, string>(), variables));
    }
}