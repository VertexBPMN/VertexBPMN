using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpSimulationServiceTests
{
	[Fact]
	public async Task SimulateAsync_PostsSimulationContract()
	{
		var requests = new List<HttpRequestMessage>();
		using var response = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = JsonContent.Create(new { simulation = new { completed = true } })
		};
		using var handler = new RecordingHandler(requests, response);
		using var client = new HttpClient(handler, disposeHandler: false)
		{
			BaseAddress = new Uri("http://api.test/")
		};
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
		var service = new HttpSimulationService(factory.Object);

		var result = await service.SimulateAsync("<definitions />", "invoice", new Dictionary<string, object?>(StringComparer.Ordinal) { ["approved"] = true }, 25, "tenant-a", TestContext.Current.CancellationToken);

		Assert.True(result.GetProperty("simulation").GetProperty("completed").GetBoolean());
		using var request = Assert.Single(requests);
		Assert.Equal(HttpMethod.Post, request.Method);
		Assert.Equal("http://api.test/api/simulation", request.RequestUri!.ToString());
		var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal("<definitions />", body.GetProperty("bpmnXml").GetString());
		Assert.Equal("invoice", body.GetProperty("processDefinitionId").GetString());
		Assert.Equal(25, body.GetProperty("maxSteps").GetInt32());
		Assert.Equal("tenant-a", body.GetProperty("tenantId").GetString());
		Assert.True(body.GetProperty("variables").GetProperty("approved").GetBoolean());
	}

	[Fact]
	public async Task GetVariableTraceAsync_PostsToEncodedAnalyticsEndpoint()
	{
		var requests = new List<HttpRequestMessage>();
		using var response = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = JsonContent.Create(new { trace = Array.Empty<object>() })
		};
		using var handler = new RecordingHandler(requests, response);
		using var client = new HttpClient(handler, disposeHandler: false)
		{
			BaseAddress = new Uri("http://api.test/")
		};
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
		var service = new HttpSimulationService(factory.Object);

		using var document = JsonDocument.Parse("{\"bpmnXml\":\"<definitions />\"}");
		await service.GetVariableTraceAsync(document.RootElement, "order/status", TestContext.Current.CancellationToken);

		using var request = Assert.Single(requests);
		Assert.Equal(HttpMethod.Post, request.Method);
		Assert.Equal("http://api.test/api/simulation-analytics/variable-trace/order%2Fstatus", request.RequestUri!.ToString());
	}

	private sealed class RecordingHandler(
		List<HttpRequestMessage> requests,
		HttpResponseMessage response) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			requests.Add(request);
			return Task.FromResult(response);
		}
	}
}
