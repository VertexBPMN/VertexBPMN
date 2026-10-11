using System.Net;
using System.Net.Http.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpPluginServiceTests
{
	[Fact]
	public async Task GetPluginsAsync_UsesPluginEndpoint()
	{
		var requests = new List<HttpRequestMessage>();
		using var response = CreateResponse(new[] { new { id = "sample" } });
		using var handler = new RecordingHandler(requests, response);
		using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
		var service = new HttpPluginService(factory.Object);

		var result = await service.GetPluginsAsync(TestContext.Current.CancellationToken);

		Assert.Equal("sample", result[0].GetProperty("id").GetString());
		var request = Assert.Single(requests);
		Assert.Equal(HttpMethod.Get, request.Method);
		Assert.Equal("http://api.test/api/plugins", request.RequestUri!.ToString());
	}

	[Fact]
	public async Task GetExtensionPointsAsync_UsesExtensionPointsEndpoint()
	{
		var requests = new List<HttpRequestMessage>();
		using var response = CreateResponse(Array.Empty<object>());
		using var handler = new RecordingHandler(requests, response);
		using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
		var service = new HttpPluginService(factory.Object);

		await service.GetExtensionPointsAsync(TestContext.Current.CancellationToken);

		var request = Assert.Single(requests);
		Assert.Equal("http://api.test/api/plugins/extension-points", request.RequestUri!.ToString());
	}

	private static HttpResponseMessage CreateResponse(object body)
	{
		var response = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = JsonContent.Create(body)
		};
		return response;
	}

	private sealed class RecordingHandler(List<HttpRequestMessage> requests, HttpResponseMessage response) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			requests.Add(request);
			return Task.FromResult(response);
		}
	}
}
