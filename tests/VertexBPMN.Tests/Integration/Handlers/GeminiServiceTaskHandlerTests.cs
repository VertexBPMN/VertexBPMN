using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shouldly;
using VertexBPMN.Application.Handlers;

namespace VertexBPMN.Tests.Integration.Handlers;

public class GeminiServiceTaskHandlerTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<GeminiServiceTaskHandler>> _loggerMock;
    private readonly GeminiServiceTaskHandler _handler;

    public GeminiServiceTaskHandlerTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<GeminiServiceTaskHandler>>();
        _handler = new GeminiServiceTaskHandler(_httpClient, _loggerMock.Object, null);
    }

    [Fact]
    public async Task ExecuteAsync_WithUseMockMode_ShouldReturnMockResponse()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "ai:model", "gemini-pro" },
            { "ai:prompt", "Analyze this data comprehensively" },
            { "ai:resultVariable", "geminiResult" },
            { "ai:useMockMode", "true" }
        };

        var variables = new Dictionary<string, object>();

        // Act
        await _handler.ExecuteAsync(attributes, variables, TestContext.Current.CancellationToken);

        // Assert
        variables.ShouldContainKey("geminiResult");
        var result = Assert.IsType<string>(variables["geminiResult"]);
        result.ShouldContain("Gemini gemini-pro processed");
        result.ShouldContain("Analyze this data comprehensively");
    }

    [Fact]
    public async Task ExecuteAsync_WithMockedHttpClient_ShouldHandleGeminiResponse()
    {
        // Arrange
        var geminiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = "Mocked Gemini response: This is a comprehensive analysis of the data." }
                        }
                    },
                    finishReason = "STOP"
                }
            },
            usageMetadata = new
            {
                promptTokenCount = 20,
                candidatesTokenCount = 12,
                totalTokenCount = 32
            }
        };

        var responseJson = JsonSerializer.Serialize(geminiResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
                    req.RequestUri!.ToString().Contains("generativelanguage.googleapis.com")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var attributes = new Dictionary<string, string>
        {
            { "ai:model", "gemini-pro" },
            { "ai:prompt", "Analyze this data comprehensively" },
            { "ai:resultVariable", "geminiResult" }
        };

        var variables = new Dictionary<string, object>();

        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-gemini-key");

        try
        {
            // Act
            await _handler.ExecuteAsync(attributes, variables, TestContext.Current.CancellationToken);

            // Assert
            variables.ShouldContainKey("geminiResult");
            var result = Assert.IsType<string>(variables["geminiResult"]);
            result.ShouldContain("Mocked Gemini response");
            result.ShouldContain("comprehensive analysis");

            // Verify HTTP request was made correctly
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("generativelanguage.googleapis.com") &&
                    req.RequestUri.ToString().Contains("test-gemini-key")),
                ItExpr.IsAny<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
        }
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("code")]
    [InlineData("analysis")]
    [InlineData("multimodal")]
    public async Task ExecuteAsync_WithDifferentTaskTypes_ShouldProcessCorrectly(string taskType)
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "ai:taskType", taskType },
            { "ai:prompt", $"Execute {taskType} task" },
            { "ai:resultVariable", "taskOutput" },
            { "ai:useMockMode", "true" }
        };

        var variables = new Dictionary<string, object>();

        // Act
        await _handler.ExecuteAsync(attributes, variables, TestContext.Current.CancellationToken);

        // Assert
        variables.ShouldContainKey("taskOutput");
        var result = Assert.IsType<string>(variables["taskOutput"]);
        result.ShouldContain("Gemini");
        result.ShouldContain($"Execute {taskType} task");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
