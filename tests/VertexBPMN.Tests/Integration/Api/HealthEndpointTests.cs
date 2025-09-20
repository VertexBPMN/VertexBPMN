using System.Net;
using System.Text.Json;

namespace VertexBPMN.Tests.Integration.Api;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk_AndContainsServiceData()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        // Adapt to actual response shape (default HealthChecks UI format or your custom)
        Assert.True(json.Contains("service_deps"), "Expected custom health check entry.");
    }
}