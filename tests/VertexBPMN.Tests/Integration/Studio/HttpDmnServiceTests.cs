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
        var client = CreateClient(requests, new[] { new { key = "approval" } });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpDmnService(factory.Object);

        var result = await service.ListDefinitionsAsync("approval", "tenant/a");

        Assert.Equal("approval", result[0].GetProperty("key").GetString());
        var request = Assert.Single(requests);
        Assert.Equal("http://api.test/api/vertex/decision-definition?key=approval&tenantId=tenant%2Fa", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task ListInstancesAsync_UsesDecisionKeyQuery()
    {
        var requests = new List<HttpRequestMessage>();
        var client = CreateClient(requests, Array.Empty<object>());
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpDmnService(factory.Object);

        await service.ListInstancesAsync("approval");

        var request = Assert.Single(requests);
        Assert.Equal("http://api.test/api/vertex/decision-instance?decisionKey=approval", request.RequestUri!.ToString());
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
