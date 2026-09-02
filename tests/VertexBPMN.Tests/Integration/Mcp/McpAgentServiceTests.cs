using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using VertexBPMN.Application;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Mcp;
public class McpAgentServiceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly string _agentFilePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "agents.json");
    private readonly HttpClient _client;

    public McpAgentServiceTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CallAgentAsync_ReturnsResponse()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(_agentFilePath)
            .Build();
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"result\":\"ok\"}", Encoding.UTF8, "application/json")
            });
        var service = new McpAgentService(configuration, new HttpClient(handler.Object));
        var input = new JsonObject { ["input"] = "Test" };
        var resp = await service.CallAgentAsync("NLP", input, TestContext.Current.CancellationToken);
        Assert.NotNull(resp);
    }

    [Fact]
    public async Task WaitForAgentResponseAsync_ReturnsDemoResponse()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(_agentFilePath)
            .Build();
        var service = new McpAgentService(configuration);
        var resp = await service.WaitForAgentResponseAsync("corr-123", TestContext.Current.CancellationToken);
        Assert.Equal("corr-123", resp["correlationId"]!.ToString());
        Assert.Equal("DemoResponse", resp["result"]!.ToString());
    }
}
