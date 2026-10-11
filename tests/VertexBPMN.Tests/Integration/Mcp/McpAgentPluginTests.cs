using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Moq.Protected;
using VertexBPMN.Api.Plugins;

namespace VertexBPMN.Tests.Integration.Mcp;

public class McpAgentPluginTests
{
	private static readonly string _agentFilePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "agents.json");

	[Theory]
	[InlineData("{\"result\":\"ok\"}", false)]
	[InlineData("{\"result\":null}", true)]
	public async Task ServiceTaskExecutesAgent(string responseJson, bool expectsNull)
	{
		var handler = new Mock<HttpMessageHandler>();
		using var response = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
		};
		handler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(response);
		using var httpClient = new HttpClient(handler.Object);
		using var plugin = new McpAgentPlugin.McpAgentPlugin(httpClient);
		var context = new PluginContext
		{
			Configuration = new TestConfig(),
			Logger = new TestLogger(),
			ExtensionPoints = []
		};
		await plugin.InitializeAsync(context);
		var actCtx = new ActivityExecutionContext
		{
			Configuration = new Dictionary<string, object>(StringComparer.Ordinal) { { "agentName", "NLP" } },
			Variables = new Dictionary<string, object>(StringComparer.Ordinal) { { "input", "Test" } }
		};
		var result = await plugin.ExecuteAsync(actCtx);
		Assert.True(result.Success);
		Assert.NotNull(result.OutputVariables);
		Assert.True(result.OutputVariables.ContainsKey("result"));
		Assert.Equal(expectsNull, result.OutputVariables["result"] is null);
	}

	[Fact]
	public async Task DisposingPluginDoesNotDisposeInjectedClient()
	{
		var handler = new Mock<HttpMessageHandler>();
		using var response = new HttpResponseMessage(HttpStatusCode.OK);
		handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
			ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).ReturnsAsync(response);
		using var httpClient = new HttpClient(handler.Object);
		using var plugin = new McpAgentPlugin.McpAgentPlugin(httpClient);
		plugin.Dispose();
		using var received = await httpClient.GetAsync(new Uri("https://example.invalid/ownership"), TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.OK, received.StatusCode);
	}

	private sealed class TestLogger : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			// This plugin fixture does not capture logs; behavior is asserted through its result.
		}
	}
	private sealed class TestConfig : IConfiguration
	{
		public string? this[string key]
		{
			get => _agentFilePath;
			// Keep this fixed-path fixture immutable without changing IConfiguration's contract.
			set => _ = value;
		}
		public IEnumerable<IConfigurationSection> GetChildren() => [];
		public IChangeToken GetReloadToken() => null!;
		public IConfigurationSection GetSection(string key) => null!;
	}
}
