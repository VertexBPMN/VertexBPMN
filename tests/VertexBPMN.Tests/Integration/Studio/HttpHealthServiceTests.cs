using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpHealthServiceTests
{
    [Fact]
    public async Task NonJsonError_IsReportedAsHttpError_BeforeDeserialization()
    {
        using var client = new HttpClient(new UnavailableHandler()) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpHealthService(factory.Object, NullLogger<HttpHealthService>.Instance);
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetComprehensiveHealthAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("temporarily unavailable") });
    }
}
