using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using VertexBPMN.Application;

namespace VertexBPMN.Tests.Integration.Mcp;

public sealed class McpHttpResourceLifetimeTests
{
	[Theory]
	[InlineData(HttpStatusCode.OK)]
	[InlineData(HttpStatusCode.BadGateway)]
	public async Task CallAgentAsync_DisposesHttpContentAfterSuccessOrFailure(HttpStatusCode status)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(
			new Dictionary<string, string?>
			{
				["McpAgents:0:name"] = "test",
				["McpAgents:0:url"] = "https://example.org/mcp"
			}).Build();
		using var handler = new RecordingHandler(status);
		using var httpClient = new HttpClient(handler);
		var service = new McpAgentService(configuration, httpClient);

		if (status == HttpStatusCode.OK)
		{
			var result = await service.CallAgentAsync("test", new JsonObject(), TestContext.Current.CancellationToken);
			Assert.Equal("ok", result["result"]!.GetValue<string>());
		}
		else
		{
			await Assert.ThrowsAsync<HttpRequestException>(() =>
				service.CallAgentAsync("test", new JsonObject(), TestContext.Current.CancellationToken));
		}

		Assert.True(handler.ResponseContent.IsDisposed);
		var requestContent = Assert.IsType<StringContent>(handler.RequestContent);
		await Assert.ThrowsAsync<ObjectDisposedException>(() => requestContent.ReadAsStringAsync(TestContext.Current.CancellationToken));
	}

	private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
	{
		public HttpContent? RequestContent { get; private set; }
		public TrackedContent ResponseContent { get; } = new();

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			RequestContent = request.Content;
			return Task.FromResult(new HttpResponseMessage(status) { Content = ResponseContent });
		}
	}

	private sealed class TrackedContent : StringContent
	{
		public TrackedContent() : base("{\"result\":\"ok\"}", Encoding.UTF8, "application/json") { }
		public bool IsDisposed { get; private set; }

		protected override void Dispose(bool disposing)
		{
			IsDisposed = true;
			base.Dispose(disposing);
		}
	}
}
