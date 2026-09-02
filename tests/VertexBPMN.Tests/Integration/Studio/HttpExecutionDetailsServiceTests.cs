using System.Net;
using System.Net.Http.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpExecutionDetailsServiceTests
{
    [Fact]
    public async Task GetVariablesAsync_UsesProcessInstanceQuery()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { approved = true })
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpExecutionDetailsService(factory.Object);
        var processInstanceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var result = await service.GetVariablesAsync(processInstanceId, TestContext.Current.CancellationToken);

        Assert.True(result.GetProperty("approved").GetBoolean());
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"http://api.test/api/vertex/variable?processInstanceId={processInstanceId}", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetJobsAsync_UsesJobsEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        };
        var client = new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpExecutionDetailsService(factory.Object);

        await service.GetJobsAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal("http://api.test/api/vertex/job", request.RequestUri!.ToString());
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
