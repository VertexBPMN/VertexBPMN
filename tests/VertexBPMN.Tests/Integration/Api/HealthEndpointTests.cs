using System.Net;
using System.Text.Json;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public class HealthEndpointTests
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly CustomWebApplicationFactory _factory;

    public HealthEndpointTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        _client = factory.WithSharedFixture(dbFixture).CreateClient(output);
    }


    [Fact]
    public async Task HealthEndpoint_ReturnsOk_AndContainsServiceData()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        // Adapt to actual response shape (default HealthChecks UI format or your custom)
        Assert.True(json.Contains("healthy"), "Expected custom health check entry.");
    }
}