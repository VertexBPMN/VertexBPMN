using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpSimulationScenarioServiceTests
{
    [Fact]
    public async Task GetAllAsync_AddsTenantQuery()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpSimulationScenarioService(factory.Object);

        await service.GetAllAsync("tenant/a");

        var request = Assert.Single(requests);
        Assert.Equal("http://api.test/api/simulation-scenario?tenantId=tenant%2Fa", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateAsync_PostsScenarioContract()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { id = "scenario-1" })
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpSimulationScenarioService(factory.Object);

        var result = await service.CreateAsync(new { name = "invoice" });

        Assert.Equal("scenario-1", result.GetProperty("id").GetString());
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://api.test/api/simulation-scenario", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetByIdAsync_UsesScenarioIdRoute()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = "scenario/1" })
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpSimulationScenarioService(factory.Object);

        await service.GetByIdAsync("scenario/1");

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://api.test/api/simulation-scenario/scenario%2F1", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task UpdateAsync_PutsScenarioContract()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = "scenario-1" })
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpSimulationScenarioService(factory.Object);

        await service.UpdateAsync("scenario-1", new { name = "updated" });

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("http://api.test/api/simulation-scenario/scenario-1", request.RequestUri!.ToString());
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
