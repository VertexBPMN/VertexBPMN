using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Application.Handlers;

namespace VertexBPMN.Tests.Integration.Handlers;

public class RejectPolicyServiceTaskHandlerTests
{
	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1873", Justification = "ILogger.Log is an expression tree passed to Moq.Verify, not an executed logging call; matcher evaluation must not be conditionally skipped.")]
	public async Task ExecuteAsync_ShouldRejectPolicySuccessfully()
	{
		// Arrange
		var loggerMock = new Mock<ILogger<RejectPolicyServiceTaskHandler>>();
		loggerMock.Setup(logger => logger.IsEnabled(LogLevel.Information)).Returns(true);
		var handler = new RejectPolicyServiceTaskHandler(loggerMock.Object);
		var variables = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				{ "policyId", "POL12345" },
				{ "reason", "High risk" }
			};

		// Act
		await handler.ExecuteAsync(new Dictionary<string, string>(StringComparer.Ordinal), variables, TestContext.Current.CancellationToken);

		// Assert
		Assert.True(variables.ContainsKey("policyStatus"));
		Assert.Equal("Rejected", variables["policyStatus"]);
		loggerMock.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, _) => v != null && (v.ToString() ?? string.Empty).Contains("POL12345", StringComparison.Ordinal)),
				It.IsAny<Exception?>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Exactly(2));
	}

	[Fact]
	public async Task ExecuteAsync_ShouldThrowException_WhenPolicyIdIsMissing()
	{
		// Arrange
		var loggerMock = new Mock<ILogger<RejectPolicyServiceTaskHandler>>();
		var handler = new RejectPolicyServiceTaskHandler(loggerMock.Object);
		var variables = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				{ "reason", "High risk" }
			};

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			handler.ExecuteAsync(new Dictionary<string, string>(StringComparer.Ordinal), variables, TestContext.Current.CancellationToken));
	}
}
