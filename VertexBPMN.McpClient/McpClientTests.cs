using System.Threading.Tasks;
using Xunit;

namespace VertexBPMN.McpClient.Tests;

public class McpClientTests
{
    [Fact]
    public async Task CanCallListProcesses()
    {
        var client = new McpClient("https://localhost:5001");
        var token = "<JWT-Token>"; // Test-Token einfügen
        var result = await client.CallJsonRpcAsync("bpmn.listProcesses", null, token);
        Assert.NotNull(result);
        Assert.True(result.ToString().Contains("invoice"));
    }
}
