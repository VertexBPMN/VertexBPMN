using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using VertexBPMN.Application.Handlers;

namespace VertexBPMN.Tests.Integration.Handlers;

public sealed class ContextEnrichmentServiceTaskHandlerTests : IDisposable
{
	public void Dispose() => _httpClient.Dispose();

	private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
	private readonly HttpClient _httpClient;

	public ContextEnrichmentServiceTaskHandlerTests()
	{
		_httpMessageHandlerMock = new Mock<HttpMessageHandler>();
		_httpClient = new HttpClient(_httpMessageHandlerMock.Object);
	}

	[Theory]
	[InlineData("customer", "customer123")]
	[InlineData("order", "order456")]
	[InlineData("product", "product789")]
	[InlineData("account", "account101")]
	public async Task ExecuteAsync_WithDifferentDataTypes_ShouldEnrichAppropriately(string dataType, string entityId)
	{
		// Arrange
		var logger = new Mock<ILogger<ContextEnrichmentServiceTaskHandler>>();
		var handler = new ContextEnrichmentServiceTaskHandler(_httpClient, logger.Object, OpenTelemetry.Trace.TracerProvider.Default);

		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "context:sourceType", "mock" },
			{ "context:dataType", dataType },
			{ "context:resultVariable", $"{dataType}Context" }
		};

		var variables = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			{ "entityId", entityId }
		};

		// Act
		await handler.ExecuteAsync(attributes, variables, TestContext.Current.CancellationToken);

		// Assert
		variables.ShouldContainKey($"{dataType}Context");
		var result = Assert.IsType<string>(variables[$"{dataType}Context"]);
		result.ShouldContain(dataType);
		result.ShouldContain(entityId);
	}
}
