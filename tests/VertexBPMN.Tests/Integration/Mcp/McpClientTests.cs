using System.Net;
using System.Text;
using Moq;
using Moq.Protected;

namespace VertexBPMN.Tests.Integration.Mcp;

public class McpClientTests
{
    [Fact]
    public async Task CanCallListProcesses()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Post &&
                    request.RequestUri!.AbsoluteUri == "http://localhost:5000/mcp/jsonrpc" &&
                    request.Headers.Authorization!.Parameter == "<JWT-Token>"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"jsonrpc\":\"2.0\",\"id\":\"test\",\"result\":[{\"key\":\"invoice\"}]}",
                    Encoding.UTF8,
                    "application/json")
            });
        using var httpClient = new HttpClient(handler.Object);
        using var client = new McpClient.McpClient(new Uri("http://localhost:5000"), httpClient);
        var token = "<JWT-Token>"; // Test-Token einfügen
        var result = await client.CallJsonRpcAsync("bpmn.listProcesses", null, token, TestContext.Current.CancellationToken);
        Assert.Contains("invoice", result.ToString(), StringComparison.Ordinal);
        Assert.Equal("invoice", result.GetProperty("result")[0].GetProperty("key").GetString());
    }

    [Fact]
    public async Task DisposingMcpClient_DoesNotDisposeInjectedHttpClient()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        using var httpClient = new HttpClient(handler.Object);
        using (var client = new McpClient.McpClient(new Uri("https://example.org"), httpClient))
        {
            await client.CallJsonRpcAsync("test", cancellationToken: TestContext.Current.CancellationToken);
        }
        using var response = await httpClient.GetAsync(new Uri("https://example.org"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
