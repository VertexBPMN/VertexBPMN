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
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { simulation = new { completed = true } })
        };
        var client = new HttpClient(new RecordingHandler(requests, response))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpSimulationService(factory.Object);

        var result = await service.SimulateAsync("<definitions />", "invoice", new Dictionary<string, object?> { ["approved"] = true }, 25, "tenant-a", TestContext.Current.CancellationToken);

        Assert.True(result.GetProperty("simulation").GetProperty("completed").GetBoolean());
        var request = Assert.Single(requests);
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
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { trace = Array.Empty<object>() })
        };
        var client = new HttpClient(new RecordingHandler(requests, response))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpSimulationService(factory.Object);

        await service.GetVariableTraceAsync(JsonDocument.Parse("{\"bpmnXml\":\"<definitions />\"}").RootElement, "order/status", TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
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
