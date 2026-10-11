using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Application.Handlers;

namespace VertexBPMN.Tests.Integration.Handlers;

public class CancelApplicationServiceTaskHandlerTests
{
	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1873", Justification = "ILogger.Log is an expression tree passed to Moq.Verify, not an executed logging call; matcher evaluation must not be conditionally skipped.")]
	public async Task ExecuteAsync_ShouldCancelApplicationSuccessfully()
	{
		// Arrange
		var loggerMock = new Mock<ILogger<CancelApplicationServiceTaskHandler>>();
		loggerMock.Setup(logger => logger.IsEnabled(LogLevel.Information)).Returns(true);

		var handler = new CancelApplicationServiceTaskHandler(loggerMock.Object);
		var variables = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			{ "applicationId", "12345" },
			{ "reason", "Customer request" }
		};

		// Act
		await handler.ExecuteAsync(new Dictionary<string, string>(StringComparer.Ordinal), variables, TestContext.Current.CancellationToken);

		// Assert
		Assert.True(variables.ContainsKey("applicationStatus"));
		Assert.Equal("Cancelled", variables["applicationStatus"]);
		loggerMock.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, _) => v != null && (v.ToString() ?? string.Empty).Contains("12345", StringComparison.Ordinal)),
				It.IsAny<Exception?>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Exactly(2));
	}

	[Fact]
	public async Task ExecuteAsync_ShouldThrowException_WhenApplicationIdIsMissing()
	{
		// Arrange
		var loggerMock = new Mock<ILogger<CancelApplicationServiceTaskHandler>>();
		var handler = new CancelApplicationServiceTaskHandler(loggerMock.Object);
		var variables = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			{ "reason", "Customer request" }
		};

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			handler.ExecuteAsync(new Dictionary<string, string>(StringComparer.Ordinal), variables, TestContext.Current.CancellationToken));
	}
}
