using System.Net;
using VertexBPMN.Api.Middleware;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public sealed class CorrelationIdApiTests(
    CustomWebApplicationFactory factory,
    SharedSqliteDbFixture fixture,
    ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Phase3Acceptance")]
    public async Task P3_AC_08_Correlation_id_is_preserved_on_health_response()
    {
        using var client = factory.WithSharedFixture(fixture).CreateClient(output);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "phase3-correlation-42");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("phase3-correlation-42", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }
}
