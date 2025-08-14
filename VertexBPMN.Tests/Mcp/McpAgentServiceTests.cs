using Xunit;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Testing;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Tests.Mcp;
public class McpAgentServiceTests : IClassFixture<WebApplicationFactory<VertexBPMN.Api.Program>>
{
    private readonly string _agentFilePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "agents.json");
    private readonly HttpClient _client;

    public McpAgentServiceTests(WebApplicationFactory<VertexBPMN.Api.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(Skip = "Needs external service")]
    public async Task CallAgentAsync_ReturnsResponse()
    {
        var service = new McpAgentService(_agentFilePath);
        var input = new JsonObject { ["input"] = "Test" };
        var resp = await service.CallAgentAsync("NLP", input, CancellationToken.None);
        Assert.NotNull(resp);
    }

    [Fact(Skip = "Needs external service")]
    public async Task WaitForAgentResponseAsync_ReturnsDemoResponse()
    {
        var service = new McpAgentService(_agentFilePath);
        var resp = await service.WaitForAgentResponseAsync("corr-123", CancellationToken.None);
        Assert.Equal("corr-123", resp["correlationId"]!.ToString());
        Assert.Equal("DemoResponse", resp["result"]!.ToString());
    }
}
