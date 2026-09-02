using System.Net;
using System.Net.Http.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpIdentityServiceTests
{
    [Fact]
    public async Task ListTenantsAsync_UsesIdentityEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var client = new HttpClient(new RecordingHandler(requests, new[]
        {
            new { id = "tenant-a", name = "Tenant A" },
            new { id = "tenant-b", name = "Tenant B" }
        }))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpIdentityService(factory.Object);

        var tenants = await service.ListTenantsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, tenants.Count);
        Assert.Equal("tenant-a", tenants[0].Id);
        Assert.Equal("Tenant B", tenants[1].Name);
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://api.test/api/identity/list-tenants", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task TenantMutations_UseTenantEndpoints()
    {
        var requests = new List<HttpRequestMessage>();
        var client = new HttpClient(new RecordingHandler(requests, new StudioTenant("tenant-1", "Tenant 1")))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpIdentityService(factory.Object);

        await service.CreateTenantAsync("Tenant 1", "Description", TestContext.Current.CancellationToken);
        await service.UpdateTenantAsync("tenant-1", "Tenant 2", "Updated", TestContext.Current.CancellationToken);
        await service.DeleteTenantAsync("tenant-1", TestContext.Current.CancellationToken);

        Assert.Collection(
            requests,
            request => AssertRequest(request, HttpMethod.Post, "/api/tenant"),
            request => AssertRequest(request, HttpMethod.Put, "/api/tenant/tenant-1"),
            request => AssertRequest(request, HttpMethod.Delete, "/api/tenant/tenant-1"));
    }

    private static void AssertRequest(HttpRequestMessage request, HttpMethod method, string path)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal($"http://api.test{path}", request.RequestUri!.ToString());
    }

    private sealed class RecordingHandler(
        List<HttpRequestMessage> requests,
        object responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseBody)
            });
        }
    }
}
