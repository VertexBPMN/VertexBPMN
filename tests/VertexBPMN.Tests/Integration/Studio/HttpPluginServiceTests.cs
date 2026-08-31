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
        var client = CreateClient(requests, new[] { new { id = "sample" } });
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
        var client = CreateClient(requests, Array.Empty<object>());
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpPluginService(factory.Object);

        await service.GetExtensionPointsAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal("http://api.test/api/plugins/extension-points", request.RequestUri!.ToString());
    }

    private static HttpClient CreateClient(List<HttpRequestMessage> requests, object body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };
        return new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
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
