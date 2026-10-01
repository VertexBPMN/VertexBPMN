using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Api.Services;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Services;

public sealed class ProductionHealthMonitoringServiceTests
{
    [Fact]
    public async Task MissingRequiredDbContexts_MustNotReturnHealthy()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var result = await CreateService(services).CheckDatabaseHealthAsync();
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(5, Assert.IsType<List<string>>(result.Data["unhealthy_databases"]).Count);
    }

    [Theory]
    [InlineData(false, HealthStatus.Healthy)]
    [InlineData(true, HealthStatus.Unhealthy)]
    public async Task AllFiveConcreteDbContexts_AreChecked(bool unavailableBpmn, HealthStatus expected)
    {
        var registrations = new ServiceCollection();
        // Read-write mode fails without creating the deliberately absent database.
        var missingFile = Path.Combine(Path.GetTempPath(), $"missing-health-{Guid.NewGuid():N}.db");
        registrations.AddDbContext<BpmnDbContext>(options => options.UseSqlite(unavailableBpmn
            ? $"Data Source={missingFile};Mode=ReadWrite" : "Data Source=:memory:"));
        registrations.AddDbContext<TenantDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        registrations.AddDbContext<SimulationScenarioDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        registrations.AddDbContext<ProcessMiningEventDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        registrations.AddDbContext<DecisionDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        using var services = registrations.BuildServiceProvider();
        var result = await CreateService(services).CheckDatabaseHealthAsync();
        Assert.Equal(expected, result.Status);
        Assert.Equal(unavailableBpmn ? 4 : 5, Assert.IsType<List<string>>(result.Data["healthy_databases"]).Count);
        if (unavailableBpmn)
            Assert.Contains("BpmnDbContext", Assert.Single(Assert.IsType<List<string>>(result.Data["unhealthy_databases"])));
        Assert.False(File.Exists(missingFile));
    }

    [Fact]
    public async Task Uptime_UsesTheProcessUtcStartTime_AndIsNeverNegative()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var process = Process.GetCurrentProcess();
        var before = DateTime.UtcNow - process.StartTime.ToUniversalTime();
        var metrics = await CreateService(services).GetSystemMetricsAsync();
        var after = DateTime.UtcNow - process.StartTime.ToUniversalTime();
        Assert.InRange(metrics.UptimeSeconds, Math.Max(0, before.TotalSeconds), after.TotalSeconds);
    }

    private static ProductionHealthMonitoringService CreateService(IServiceProvider services) =>
        new(NullLogger<ProductionHealthMonitoringService>.Instance, services);
}
