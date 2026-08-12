using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using VertexBPMN.Application.Handlers;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Tests.Integration.Handlers;

public class AiServiceTaskHandlerTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<AIServiceTaskHandler>> _loggerMock;
    private readonly AIServiceTaskHandler _handler;

    public AiServiceTaskHandlerTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_MockProvider_ShouldReturnExpectedResult()
    {
        // Arrange
        var logger = new Mock<ILogger<AIServiceTaskHandler>>();
        var handler = new AIServiceTaskHandler(logger.Object, _httpClient);

        var attributes = new Dictionary<string, string>
        {
            { "ai:provider", "mock" },
            { "ai:model", "test-model" },
            { "ai:prompt", "Test prompt" },
            { "ai:resultVariable", "testResult" }
        };

        var variables = new Dictionary<string, object>
        {
            { "customerId", "123" }
        };

        // Act
        await handler.ExecuteAsync(attributes, variables);

        // Assert
        variables.ShouldContainKey("testResult");
        variables["testResult"].ShouldNotBeNull();
        variables["testResult"].ToString().ShouldContain("Mock AI");
        variables["testResult"].ToString().ShouldContain("test-model");
        variables["testResult"].ToString().ShouldContain("Test prompt");
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedProvider_ShouldThrowException()
    {
        // Arrange
        var logger = new Mock<ILogger<AIServiceTaskHandler>>();
        var httpClient = new HttpClient();
        var handler = new AIServiceTaskHandler(logger.Object, _httpClient);

        var attributes = new Dictionary<string, string>
        {
            { "ai:provider", "unsupported-provider" },
            { "ai:prompt", "Test prompt" }
        };

        var variables = new Dictionary<string, object>();

        // Act & Assert
        var exception = await Should.ThrowAsync<ServiceTaskExecutionException>(
            () => handler.ExecuteAsync(attributes, variables));

        exception.Message.ShouldContain("Unsupported AI provider: unsupported-provider");
    }

    [Fact]
    public async Task ExecuteAsync_OnException_ShouldSetErrorVariables()
    {
        // Arrange
        var logger = new Mock<ILogger<AIServiceTaskHandler>>();
        var httpClient = new HttpClient();
        var handler = new AIServiceTaskHandler(logger.Object, _httpClient);

        var attributes = new Dictionary<string, string>
        {
            { "ai:provider", "openai" }, // Will fail without API key
            { "ai:prompt", "Test prompt" }
        };

        var variables = new Dictionary<string, object>();

        // Ensure no API key is set
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        // Act & Assert
        await Should.ThrowAsync<ServiceTaskExecutionException>(
            () => handler.ExecuteAsync(attributes, variables));

        // Verify error variables are set
        variables.ShouldContainKey("aiTask_error");
        variables.ShouldContainKey("aiTask_failed");
        variables["aiTask_failed"].ShouldBe(true);
        variables["aiTask_error"].ToString().ShouldContain("OPENAI_API_KEY");
    }
}