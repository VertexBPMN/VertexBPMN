using System.Net;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;

namespace VertexBPMN.SourceControl.Tests;

public sealed class HttpsBoundaryTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("10.0.0.1")]
    [InlineData("::1")]
    public async Task Actual_http_handler_rejects_private_dns_before_socket_or_tls(string address)
    {
        var resolutions = 0;
        using var client = SourceControlHttps.CreateClient(["api.github.com"], TimeSpan.FromSeconds(2), (_, _) =>
        {
            resolutions++;
            // A mixed answer is denied as well; a public first answer is not sufficient.
            return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse(address) });
        });
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://api.github.com/repos", TestContext.Current.CancellationToken));
        Assert.IsType<SourceControlSecurityException>(error.InnerException);
        Assert.Equal(1, resolutions);
    }
}
