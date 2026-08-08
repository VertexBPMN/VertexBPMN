using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Application.Handlers;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Integration.Mcp;

public class McpServiceTaskHandlerTests
{
    private readonly Mock<ILogger<McpServiceTaskHandler>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<TracerProvider> _tracerProviderMock;
    private readonly McpServiceTaskHandler _handler;

    public McpServiceTaskHandlerTests()
    {
        _loggerMock = new Mock<ILogger<McpServiceTaskHandler>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _tracerProviderMock = new Mock<TracerProvider>();
        _handler = new McpServiceTaskHandler(_httpClient, _loggerMock.Object, _tracerProviderMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_McpServiceTask_Successfully()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
            {
                { "mcpServerUrl", "http://cms-mcp:8080/api/mcp" },
                { "mcpMethod", "trigger_approval" },
                { "mcpParams", "{\"documentId\": \"doc123\"}" }
            };
        var variables = new Dictionary<string, object>
            {
                { "caseId", "case1" }
            };
        var responseContent = JsonSerializer.Serialize(new JsonRpcResponse("2.0", new { result = "success" }, null));
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseContent) };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        await _handler.ExecuteAsync(attributes, variables, CancellationToken.None);

        // Assert
        _httpMessageHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(request => request.RequestUri!.ToString() == "http://cms-mcp:8080/api/mcp"),
                ItExpr.IsAny<CancellationToken>());
        Assert.Contains("result", variables);
        Assert.Equal("success", variables["result"].ToString());
    }

    [Fact]
    public async Task ExecuteAsync_MissingMcpServerUrl_ThrowsException()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
            {
                { "mcpMethod", "trigger_approval" }
            };
        var variables = new Dictionary<string, object>();

        // Act & Assert
        await Assert.ThrowsAsync<DistributedTokenException>(() => _handler.ExecuteAsync(attributes, variables, CancellationToken.None));
    }
}
