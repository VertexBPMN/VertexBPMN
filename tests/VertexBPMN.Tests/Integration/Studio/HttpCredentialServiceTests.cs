using System.Net;
using System.Net.Http.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpCredentialServiceTests
{
    [Fact]
    public async Task Mutations_UseTenantScopedCredentialEndpoints()
    {
        var requests = new List<HttpRequestMessage>();
        var credential = new StudioCredential(
            "credential-1", "tenant-a", "Payments", "api-key", null, ["token"], DateTime.UtcNow, DateTime.UtcNow, null);
        var client = new HttpClient(new RecordingHandler(requests, credential))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpCredentialService(factory.Object);

        await service.ListAsync("tenant-a");
        await service.CreateAsync("tenant-a", "Payments", "api-key", null, "token", "secret");
        await service.UpdateMetadataAsync("tenant-a", "credential-1", "Payments", "api-key", "updated");
        await service.RotateSecretAsync("tenant-a", "credential-1", "token", "rotated");
        await service.DeleteAsync("tenant-a", "credential-1");

        Assert.Collection(
            requests,
            request => AssertRequest(request, HttpMethod.Get, "/api/credentials?tenantId=tenant-a"),
            request => AssertRequest(request, HttpMethod.Post, "/api/credentials"),
            request => AssertRequest(request, HttpMethod.Put, "/api/credentials/credential-1"),
            request => AssertRequest(request, HttpMethod.Put, "/api/credentials/credential-1/secret"),
            request => AssertRequest(request, HttpMethod.Delete, "/api/credentials/credential-1?tenantId=tenant-a"));
    }

    private static void AssertRequest(HttpRequestMessage request, HttpMethod method, string path)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal($"http://api.test{path}", request.RequestUri!.ToString());
    }

    private sealed class RecordingHandler(List<HttpRequestMessage> requests, StudioCredential responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(request);
            object body = request.Method == HttpMethod.Get ? new[] { responseBody } : responseBody;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
        }
    }
}
