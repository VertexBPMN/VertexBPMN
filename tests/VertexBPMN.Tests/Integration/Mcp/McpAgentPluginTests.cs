using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text;
using VertexBPMN.Api.Plugins;

namespace VertexBPMN.Tests.Integration.Mcp;
public class McpAgentPluginTests
{
     private readonly static string _agentFilePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData",  "agents.json");

     [Fact]
    public async Task ServiceTaskExecutesAgent()
    {
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
        var plugin = new McpAgentPlugin.McpAgentPlugin(new HttpClient(handler.Object));
        var context = new PluginContext
        {
            Configuration = new TestConfig(),
            Logger = new TestLogger(),
            ExtensionPoints = new List<PluginExtensionPoint>()
        };
        await plugin.InitializeAsync(context);
        var actCtx = new ActivityExecutionContext
        {
            Configuration = new Dictionary<string, object> { { "agentName", "NLP" } },
            Variables = new Dictionary<string, object> { { "input", "Test" } }
        };
        var result = await plugin.ExecuteAsync(actCtx);
        Assert.True(result.Success);
    }

    private class TestLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formatter) { }
    }
    private class TestConfig : IConfiguration
    {
        public string this[string key]
        {
            get => key == "agentsConfigPath" ? _agentFilePath : _agentFilePath;
            set { }
        }
        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => null!;
        public IConfigurationSection GetSection(string key) => null!;
    }
}
