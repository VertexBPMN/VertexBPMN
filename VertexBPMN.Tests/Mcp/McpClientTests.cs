
using System.Threading.Tasks;
using Xunit;
using VertexBPMN.Mcp;

namespace VertexBPMN.Tests.Mcp;

public class McpClientTests
{
    [Fact(Skip = "Needs external service")]
    public async Task CanCallListProcesses()
    {
        var client = new McpClient("http://localhost:5000");
        var token = "<JWT-Token>"; // Test-Token einfügen
        var result = await client.CallJsonRpcAsync("bpmn.listProcesses", null, token);
        Assert.NotNull(result);
        Assert.True(result.ToString().Contains("invoice"));
    }
}
