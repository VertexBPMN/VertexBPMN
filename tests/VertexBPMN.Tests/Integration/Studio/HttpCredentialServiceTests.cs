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

        await service.ListAsync("tenant-a", TestContext.Current.CancellationToken);
        await service.CreateAsync("tenant-a", "Payments", "api-key", null, "token", "secret", TestContext.Current.CancellationToken);
        await service.UpdateMetadataAsync("tenant-a", "credential-1", "Payments", "api-key", "updated", TestContext.Current.CancellationToken);
        await service.RotateSecretAsync("tenant-a", "credential-1", "token", "rotated", TestContext.Current.CancellationToken);
        await service.DeleteAsync("tenant-a", "credential-1", TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task StartOAuth2Authorization_PostsAuthorizeEndpointAndReturnsRedirectUrl()
    {
        HttpRequestMessage? captured = null;
        var body = string.Empty;
        var client = new HttpClient(new OAuth2AuthorizeHandler(r =>
        {
            captured = r;
            body = r.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
        }))
        { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpCredentialService(factory.Object);

        var redirectUrl = await service.StartOAuth2AuthorizationAsync(
            "tenant-a", "credential-1",
            new OAuth2ConnectConfig("https://idp.test/authorize", "https://idp.test/token", "client-1", "https://studio.test/api/oauth2/callback", "openid"),
            TestContext.Current.CancellationToken);

        Assert.Equal("https://idp.test/authorize?state=abc123", redirectUrl);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("http://api.test/api/oauth2/authorize", captured.RequestUri!.ToString());
        Assert.Contains("\"tenantId\":\"tenant-a\"", body, StringComparison.Ordinal);
        Assert.Contains("\"credentialId\":\"credential-1\"", body, StringComparison.Ordinal);
        Assert.Contains("\"clientId\":\"client-1\"", body, StringComparison.Ordinal);
        Assert.Contains("\"redirectUri\":\"https://studio.test/api/oauth2/callback\"", body, StringComparison.Ordinal);
    }

    private sealed class OAuth2AuthorizeHandler(Action<HttpRequestMessage> onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { redirectUrl = "https://idp.test/authorize?state=abc123", state = "abc123" })
            });
        }
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
