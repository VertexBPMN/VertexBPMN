using System.Net;
using System.Text.Json;
using VertexBPMN.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Entities;

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
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        // Adapt to actual response shape (default HealthChecks UI format or your custom)
        Assert.True(json.Contains("healthy"), "Expected custom health check entry.");
    }

    [Theory]
    [InlineData("/api/health/database")]
    [InlineData("/api/health/comprehensive")]
    public async Task UnhealthyException_IsSerializedAsCompleteJson_WithoutLeakingExceptionData(string path)
    {
        Exception failure;
        try { throw new InvalidOperationException("private-test-connection-password"); }
        catch (InvalidOperationException exception) { failure = exception; }
        Assert.NotNull(failure.TargetSite); // The reflection member breaks raw Exception serialization.
        var result = HealthCheckResult.Unhealthy("Database unavailable", failure,
            new Dictionary<string, object> { ["unhealthy_databases"] = new[] { "BpmnDbContext" } });
        var monitoring = new Mock<IHealthMonitoringService>();
        monitoring.Setup(service => service.CheckDatabaseHealthAsync()).ReturnsAsync(result);
        monitoring.Setup(service => service.GetComprehensiveHealthReportAsync()).ReturnsAsync(new ComprehensiveHealthReport
        {
            OverallStatus = "Unhealthy", DatabaseHealth = result, Timestamp = DateTime.UtcNow
        });
        // A dedicated host owns its migrated databases; do not re-run migrations
        // on the collection's manually created shared schema.
        await using var isolated = new CustomWebApplicationFactory().WithTestServices(services =>
        {
            services.RemoveAll<IHealthMonitoringService>();
            services.AddSingleton(monitoring.Object);
        });
        using var client = isolated.CreateClient(_output);
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var payload = JsonDocument.Parse(body);
        Assert.Contains("Database unavailable", body);
        Assert.Contains("BpmnDbContext", body);
        Assert.DoesNotContain("private-test-connection-password", body);
        Assert.DoesNotContain("targetSite", body, StringComparison.OrdinalIgnoreCase);
        var database = path.EndsWith("comprehensive", StringComparison.Ordinal) ? payload.RootElement.GetProperty("databaseHealth") : payload.RootElement;
        Assert.False(database.TryGetProperty("exception", out _));
    }
}
