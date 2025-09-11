using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Domain;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.EngineServices.Handlers;

namespace VertexBPMN.Tests.Integration.Mcp;

public class McpServiceTaskHandlerTests
{
    private readonly Mock<ILogger<McpServiceTaskHandler>> _loggerMock;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<TracerProvider> _tracerProviderMock;
    private readonly McpServiceTaskHandler _handler;

    public McpServiceTaskHandlerTests()
    {
        _loggerMock = new Mock<ILogger<McpServiceTaskHandler>>();
        _httpClientMock = new Mock<HttpClient>();
        _tracerProviderMock = new Mock<TracerProvider>();
        _handler = new McpServiceTaskHandler(_httpClientMock.Object, _loggerMock.Object, _tracerProviderMock.Object);
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

        _httpClientMock.Setup(c => c.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Act
        await _handler.ExecuteAsync(attributes, variables, CancellationToken.None);

        // Assert
        _httpClientMock.Verify(c => c.PostAsync("http://cms-mcp:8080/api/mcp", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Once());
        Assert.Contains("result", variables);
        Assert.Equal("success", variables["result"]);
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
