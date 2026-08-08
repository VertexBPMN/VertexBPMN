using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shouldly;
using VertexBPMN.Application.Handlers;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Tests.Integration.Handlers;

public class OpenAiServiceTaskHandlerTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OpenAiServiceTaskHandler>> _loggerMock;
    private readonly OpenAiServiceTaskHandler _handler;

    public OpenAiServiceTaskHandlerTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<OpenAiServiceTaskHandler>>();
        _handler = new OpenAiServiceTaskHandler(_httpClient, _loggerMock.Object, null);
    }

    [Fact]
    public async Task ExecuteAsync_WithUseMockMode_ShouldReturnMockResponse()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "ai:model", "gpt-4" },
            { "ai:prompt", "Analyze customer sentiment" },
            { "ai:resultVariable", "openAiResult" },
            { "ai:useMockMode", "true" }
        };

        var variables = new Dictionary<string, object>
        {
            { "customerId", "customer123" }
        };

        // Act
        await _handler.ExecuteAsync(attributes, variables);

        // Assert
        variables.ShouldContainKey("openAiResult");
        variables["openAiResult"].ToString().ShouldContain("OpenAI gpt-4 processed");
        variables["openAiResult"].ToString().ShouldContain("Analyze customer sentiment");
    }

    [Fact]
    public async Task ExecuteAsync_WithMockedHttpClient_ShouldHandleOpenAIResponse()
    {
        // Arrange
        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "Mocked OpenAI response: Customer sentiment is positive with 85% confidence."
                    },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 25,
                completion_tokens = 15,
                total_tokens = 40
            },
            model = "gpt-4",
            id = "chatcmpl-test123"
        };

        var responseJson = JsonSerializer.Serialize(openAiResponse, new JsonSerializerOptions
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
                    req.RequestUri!.ToString().Contains("openai.com")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var attributes = new Dictionary<string, string>
        {
            { "ai:model", "gpt-4" },
            { "ai:prompt", "Analyze customer sentiment" },
            { "ai:resultVariable", "openAiResult" }
        };

        var variables = new Dictionary<string, object>
        {
            { "customerId", "customer123" }
        };

        // Set environment variable for test
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-api-key");

        try
        {
            // Act
            await _handler.ExecuteAsync(attributes, variables);

            // Assert
            variables.ShouldContainKey("openAiResult");
            variables["openAiResult"].ToString().ShouldContain("Mocked OpenAI response");
            variables["openAiResult"].ToString().ShouldContain("positive");

            // Verify HTTP request was made correctly
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("openai.com")),
                ItExpr.IsAny<CancellationToken>());
        }
        finally
        {
            // Clean up
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        }
    }

    [Fact]
    public async Task ExecuteAsync_HttpError_ShouldThrowServiceTaskExecutionException()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Invalid API key", Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var attributes = new Dictionary<string, string>
        {
            { "ai:model", "gpt-4" },
            { "ai:prompt", "Test prompt" }
        };

        var variables = new Dictionary<string, object>();

        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "invalid-key");

        try
        {
            // Act & Assert
            var exception = await Should.ThrowAsync<ServiceTaskExecutionException>(
                () => _handler.ExecuteAsync(attributes, variables));

            exception.Message.ShouldContain("OpenAI API error: Unauthorized");
        }
        finally
        {
            // Clean up
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        }
    }

    [Theory]
    [InlineData("gpt-3.5-turbo", "Test with GPT-3.5")]
    [InlineData("gpt-4", "Test with GPT-4")]
    [InlineData("gpt-4-turbo", "Test with GPT-4 Turbo")]
    public async Task ExecuteAsync_WithDifferentModels_ShouldSendCorrectModelInRequest(string model, string prompt)
    {
        // Arrange
        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = $"Response from {model}: {prompt}"
                    },
                    finish_reason = "stop"
                }
            },
            model = model
        };

        var responseJson = JsonSerializer.Serialize(openAiResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        string? capturedRequestBody = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedRequestBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(httpResponse);

        var attributes = new Dictionary<string, string>
        {
            { "ai:model", model },
            { "ai:prompt", prompt },
            { "ai:resultVariable", $"result_{model.Replace(".", "_").Replace("-", "_")}" }
        };

        var variables = new Dictionary<string, object>();

        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");

        try
        {
            // Act
            await _handler.ExecuteAsync(attributes, variables, CancellationToken.None);

            // Assert
            var resultKey = $"result_{model.Replace(".", "_").Replace("-", "_")}";
            variables.ShouldContainKey(resultKey);
            
            // Verify the request body contains the correct model
            capturedRequestBody.ShouldNotBeNull();
            capturedRequestBody.ShouldContain($"\"model\":\"{model}\"");
            capturedRequestBody.ShouldContain(prompt);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}