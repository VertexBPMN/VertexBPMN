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
        var client = new McpClient.McpClient("http://localhost:5000", new HttpClient(handler.Object));
        var token = "<JWT-Token>"; // Test-Token einfügen
        var result = await client.CallJsonRpcAsync("bpmn.listProcesses", null, token);
        Assert.NotNull(result);
        Assert.True(result.ToString().Contains("invoice"));
    }
}
