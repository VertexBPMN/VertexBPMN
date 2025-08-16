using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Core.Handlers;
using Xunit;

namespace VertexBPMN.Tests.Tasks
{
    public class RejectPolicyServiceTaskHandlerTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldRejectPolicySuccessfully()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<RejectPolicyServiceTaskHandler>>();
            var handler = new RejectPolicyServiceTaskHandler(loggerMock.Object);
            var variables = new Dictionary<string, object>
            {
                { "policyId", "POL12345" },
                { "reason", "High risk" }
            };

            // Act
            await handler.ExecuteAsync(new Dictionary<string, string>(), variables);

            // Assert
            Assert.True(variables.ContainsKey("policyStatus"));
            Assert.Equal("Rejected", variables["policyStatus"]);
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
            var loggerMock = new Mock<ILogger<RejectPolicyServiceTaskHandler>>();
            var handler = new RejectPolicyServiceTaskHandler(loggerMock.Object);
            var variables = new Dictionary<string, object>
            {
                { "reason", "High risk" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.ExecuteAsync(new Dictionary<string, string>(), variables));
        }
    }
}