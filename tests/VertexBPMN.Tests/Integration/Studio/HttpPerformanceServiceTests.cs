using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpPerformanceServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_UsesPerformanceDashboardEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { systemMetrics = new { cpuUsage = 0.25 } })
        };
        var client = new HttpClient(new RecordingHandler(requests, response))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpPerformanceService(factory.Object);

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(0.25, dashboard.GetProperty("systemMetrics").GetProperty("cpuUsage").GetDouble());
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://api.test/api/performance/dashboard", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetTrendsAsync_UsesRequestedHours()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        };
        var client = new HttpClient(new RecordingHandler(requests, response))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpPerformanceService(factory.Object);

        await service.GetTrendsAsync(6);

        var request = Assert.Single(requests);
        Assert.Equal("http://api.test/api/performance/trends?hours=6", request.RequestUri!.ToString());
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
