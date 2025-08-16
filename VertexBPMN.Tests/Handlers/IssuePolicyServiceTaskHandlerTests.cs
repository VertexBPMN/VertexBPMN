using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Core.Handlers;
using Xunit;

namespace VertexBPMN.Tests.Tasks;

public class IssuePolicyServiceTaskHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldIssuePolicySuccessfully()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<IssuePolicyServiceTaskHandler>>();
        var handler = new IssuePolicyServiceTaskHandler(loggerMock.Object);
        var variables = new Dictionary<string, object>
        {
            { "policyId", "POL12345" },
            { "customerId", "CUST67890" }
        };

        // Act
        await handler.ExecuteAsync(new Dictionary<string, string>(), variables);

        // Assert
        Assert.True(variables.ContainsKey("policyStatus"));
        Assert.Equal("Issued", variables["policyStatus"]);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("POL12345")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenPolicyIdIsMissing()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<IssuePolicyServiceTaskHandler>>();
        var handler = new IssuePolicyServiceTaskHandler(loggerMock.Object);
        var variables = new Dictionary<string, object>
        {
            { "customerId", "CUST67890" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(new Dictionary<string, string>(), variables));
    }
}