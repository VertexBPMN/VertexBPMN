using System.Net;

namespace VertexBPMN.Tests.Integration.Applications;

public sealed class ApplicationEndpointLimitTests
{
    [Fact]
    public async Task Get_RejectsOversizedResponseWithoutPartialData()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json.Replace("\"maxResponseBytes\":4096", "\"maxResponseBytes\":1", StringComparison.Ordinal));
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
