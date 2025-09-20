using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shouldly;
using VertexBPMN.Application.Handlers;

namespace VertexBPMN.Tests.Integration.Handlers;

public class AnthropicServiceTaskHandlerTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<AnthropicServiceTaskHandler>> _loggerMock;
    private readonly AnthropicServiceTaskHandler _handler;

    public AnthropicServiceTaskHandlerTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<AnthropicServiceTaskHandler>>();
        _handler = new AnthropicServiceTaskHandler(_httpClient, _loggerMock.Object, null);
    }

    [Fact]
    public async Task ExecuteAsync_WithUseMockMode_ShouldReturnMockResponse()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "ai:model", "claude-3-sonnet-20240229" },
            { "ai:prompt", "Provide analysis and recommendation" },
            { "ai:resultVariable", "claudeResult" },
            { "ai:useMockMode", "true" }
        };

        var variables = new Dictionary<string, object>();

        // Act
        await _handler.ExecuteAsync(attributes, variables);

        // Assert
        variables.ShouldContainKey("claudeResult");
        variables["claudeResult"].ToString().ShouldContain("Claude claude-3-sonnet-20240229 processed");
        variables["claudeResult"].ToString().ShouldContain("Provide analysis and recommendation");
    }

    [Fact]
    public async Task ExecuteAsync_WithMockedHttpClient_ShouldHandleClaudeResponse()
    {
        // Arrange
        var claudeResponse = new
        {
            id = "msg_test123",
            type = "message",
            role = "assistant",
            content = new[]
            {
                new
                {
                    type = "text",
                    text = "Mocked Claude response: After careful analysis, I recommend proceeding with caution."
                }
            },
            model = "claude-3-sonnet-20240229",
            stop_reason = "end_turn",
            usage = new
            {
                input_tokens = 15,
                output_tokens = 18
            }
        };

        var responseJson = JsonSerializer.Serialize(claudeResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().Contains("anthropic.com")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var attributes = new Dictionary<string, string>
        {
            { "ai:model", "claude-3-sonnet-20240229" },
            { "ai:prompt", "Provide analysis and recommendation" },
            { "ai:resultVariable", "claudeResult" }
        };

        var variables = new Dictionary<string, object>();

        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-anthropic-key");

        try
        {
            // Act
            await _handler.ExecuteAsync(attributes, variables);

            // Assert
            variables.ShouldContainKey("claudeResult");
            variables["claudeResult"].ToString().ShouldContain("Mocked Claude response");
            variables["claudeResult"].ToString().ShouldContain("careful analysis");

            // Verify HTTP request headers
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("anthropic.com")),
                ItExpr.IsAny<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}