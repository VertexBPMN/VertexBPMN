using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpMessageSignalServiceTests
{
	[Fact]
	public async Task CorrelateMessageAsync_PostsMessageRoute()
	{
		var requests = new List<HttpRequestMessage>();
		using var handler = new RecordingHandler(requests, "{\"resultType\":\"Execution\"}");
		using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
		var service = CreateService(client);

		await service.CorrelateMessageAsync("payment-received", "process-1", "{\"amount\":42}", TestContext.Current.CancellationToken);

		var request = Assert.Single(requests);
		Assert.Equal(HttpMethod.Post, request.Method);
		Assert.Equal("http://api.test/api/vertex/message", request.RequestUri!.ToString());
		var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal("payment-received", body.GetProperty("messageName").GetString());
		Assert.Equal(42, body.GetProperty("variables").GetProperty("amount").GetInt32());
	}

	[Fact]
	public async Task BroadcastSignalAsync_AcceptsEmptySuccessResponse()
	{
		var requests = new List<HttpRequestMessage>();
		using var handler = new RecordingHandler(requests, string.Empty);
		using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
		var service = CreateService(client);

		var result = await service.BroadcastSignalAsync("order-updated", cancellationToken: TestContext.Current.CancellationToken);

		var request = Assert.Single(requests);
		Assert.Equal(HttpMethod.Post, request.Method);
		Assert.Equal("http://api.test/api/vertex/signal", request.RequestUri!.ToString());
		Assert.True(result.GetProperty("success").GetBoolean());
	}

	private static HttpMessageSignalService CreateService(HttpClient client)
	{
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
		return new HttpMessageSignalService(factory.Object);
	}

	private sealed class RecordingHandler(List<HttpRequestMessage> requests, string responseBody) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			requests.Add(request);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(responseBody)
			});
		}
	}
}
