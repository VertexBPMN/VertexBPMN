using System.Net;
using System.Net.Http.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpDmnServiceTests
{
	[Fact]
	public async Task ListDefinitionsAsync_UsesKeyAndTenantQuery()
	{
		var requests = new List<HttpRequestMessage>();
		using var response = CreateResponse(new[] { new { key = "approval" } });
		using var handler = new RecordingHandler(requests, response);
		using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
		var service = new HttpDmnService(factory.Object);

		var result = await service.ListDefinitionsAsync("approval", "tenant/a", TestContext.Current.CancellationToken);

		Assert.Equal("approval", result[0].GetProperty("key").GetString());
		var request = Assert.Single(requests);
		Assert.Equal("http://api.test/api/vertex/decision-definition?key=approval&tenantId=tenant%2Fa", request.RequestUri!.ToString());
	}

	[Fact]
	public async Task ListInstancesAsync_UsesDecisionKeyQuery()
	{
		var requests = new List<HttpRequestMessage>();
		using var response = CreateResponse(Array.Empty<object>());
		using var handler = new RecordingHandler(requests, response);
		using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
		var service = new HttpDmnService(factory.Object);

		await service.ListInstancesAsync("approval", cancellationToken: TestContext.Current.CancellationToken);

		var request = Assert.Single(requests);
		Assert.Equal("http://api.test/api/vertex/decision-instance?decisionKey=approval", request.RequestUri!.ToString());
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
