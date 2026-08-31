using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using Shouldly;
using VertexBPMN.Application.Handlers;

namespace VertexBPMN.Tests.Integration.Handlers;

public class GenericAiServiceTaskHandlerTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;

    public GenericAiServiceTaskHandlerTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

    }

    [Theory]
    [InlineData("openai", "gpt-4")]
    [InlineData("anthropic", "claude-3-sonnet")]
    [InlineData("cohere", "command-r")]
    [InlineData("huggingface", "llama-2-7b")]
    [InlineData("ollama", "llama3")]
    [InlineData("custom", "my-model")]
    public async Task ExecuteAsync_WithDifferentProviders_ShouldReflectProviderInResult(string provider, string model)
    {
        // Arrange
        var logger = new Mock<ILogger<GenericAiServiceTaskHandler>>();
        var handler = new GenericAiServiceTaskHandler(_httpClient, logger.Object, TracerProvider.Default);

        var attributes = new Dictionary<string, string>
        {
            { "ai:provider", provider },
            { "ai:model", model },
            { "ai:prompt", $"Test {provider} provider" },
            { "ai:resultVariable", $"{provider}Result" }
        };

        var variables = new Dictionary<string, object>();

        // Act
        await handler.ExecuteAsync(attributes, variables, TestContext.Current.CancellationToken);

        // Assert
        variables.ShouldContainKey($"{provider}Result");
        var result = Assert.IsType<string>(variables[$"{provider}Result"]);
        result.ShouldContain(provider);
        result.ShouldContain(model);
        result.ShouldContain($"Test {provider} provider");
    }
}
