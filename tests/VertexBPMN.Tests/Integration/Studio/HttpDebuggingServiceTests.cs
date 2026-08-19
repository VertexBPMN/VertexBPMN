using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpDebuggingServiceTests
{
    [Fact]
    public async Task StepOverAsync_PostsToVisualDebugSessionEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { sessionId = "session" })
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpDebuggingService(factory.Object);
        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await service.StepOverAsync(sessionId);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"http://api.test/api/visual-debug/step/over/{sessionId}", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetProcessVisualizationAsync_UsesVisualizationEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { processInstanceId = "instance" })
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpDebuggingService(factory.Object);
        var processInstanceId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await service.GetProcessVisualizationAsync(processInstanceId);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"http://api.test/api/visual-debug/visualize/{processInstanceId}", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetExecutionTraceAsync_UsesPersistentTraceEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { events = Array.Empty<object>() })
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpDebuggingService(factory.Object);
        var processInstanceId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await service.GetExecutionTraceAsync(processInstanceId);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"http://api.test/api/visual-debug/trace/{processInstanceId}", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task InspectVariablesAsync_UsesSessionVariablesEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { variables = new { approved = true } })
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpDebuggingService(factory.Object);
        var sessionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        await service.InspectVariablesAsync(sessionId);

        var request = Assert.Single(requests);
        Assert.Equal($"http://api.test/api/visual-debug/variables/{sessionId}", request.RequestUri!.ToString());
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
