using System.Net;
using System.Net.Http.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpConnectorServiceTests
{
    [Fact]
    public async Task Mutations_UseTenantScopedConnectorEndpoints()
    {
        var requests = new List<HttpRequestMessage>();
        var connector = new StudioConnector("connector-1", "tenant-a", "Payments", "http", null,
            "https://payments.example.test", "credential-1", null, true, DateTime.UtcNow, DateTime.UtcNow);
        var client = new HttpClient(new RecordingHandler(requests, connector)) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpConnectorService(factory.Object);

        await service.ListAsync("tenant-a", TestContext.Current.CancellationToken);
        await service.CreateAsync("tenant-a", "Payments", "http", null, "https://payments.example.test", "credential-1", null, cancellationToken: TestContext.Current.CancellationToken);
        await service.UpdateAsync("tenant-a", "connector-1", "Payments", "http", "updated", null, null, null, false, TestContext.Current.CancellationToken);
        await service.SetEnabledAsync("tenant-a", "connector-1", false, TestContext.Current.CancellationToken);
        await service.DeleteAsync("tenant-a", "connector-1", TestContext.Current.CancellationToken);

        Assert.Collection(requests,
            request => AssertRequest(request, HttpMethod.Get, "/api/connectors?tenantId=tenant-a"),
            request => AssertRequest(request, HttpMethod.Post, "/api/connectors"),
            request => AssertRequest(request, HttpMethod.Put, "/api/connectors/connector-1"),
            request => AssertRequest(request, HttpMethod.Put, "/api/connectors/connector-1/enabled"),
            request => AssertRequest(request, HttpMethod.Delete, "/api/connectors/connector-1?tenantId=tenant-a"));
    }

    private static void AssertRequest(HttpRequestMessage request, HttpMethod method, string path)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal($"http://api.test{path}", request.RequestUri!.ToString());
    }

    private sealed class RecordingHandler(List<HttpRequestMessage> requests, StudioConnector responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(request);
            object body = request.Method == HttpMethod.Get ? new[] { responseBody } : responseBody;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
        }
    }
}
