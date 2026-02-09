using Microsoft.Extensions.Configuration;
using VertexBPMN.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using VertexBPMN.Api;
using VertexBPMN.Api.Health;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine;
using Xunit;

[Collection("IntegratedApi")]
public class ServiceDependenciesHealthCheckTests
{
    private ServiceDependenciesHealthCheck Create(params (Type serviceType, object impl)[] overrides)
    {
        var sc = new ServiceCollection();

        // Use ConfigurationBuilder to create an IConfiguration instance
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "InMemory",
                ["Database:ConnectionString"] = "Filename=:memory:"
            })
            .Build();

        sc.AddApiServices(configuration);
        sc.AddAllEngineDbContexts(configuration);
        sc.AddBpmnPersistenceServices(configuration);
        sc.AddApplicationServices(configuration);
        sc.AddEngineServices(configuration);

        var sp = sc.BuildServiceProvider();
        return new ServiceDependenciesHealthCheck(sp);
    }

    [Fact]
    public async Task AllServicesPresent_ReturnsHealthy()
    {
        var hc = Create();
        var ctx = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("service_deps", hc, null, [])
        };

        var result = await hc.CheckHealthAsync(ctx, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("All critical services resolved", result.Description);
    }

    [Fact]
    public async Task MissingService_ReturnsUnhealthy_WithMissingList()
    {
        // Omit IHistoryService
        var hc = Create((typeof(IHistoryService), null!)); // overwriting with null registration ignored -> missing
        var ctx = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("service_deps", hc, null, [])
        };

        var result = await hc.CheckHealthAsync(ctx);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Missing", result.Description);
        Assert.Contains("missingServices", result.Data.Keys);
        var missing = (System.Collections.IEnumerable)result.Data["missingServices"]!;
        Assert.Contains("IHistoryService", missing.Cast<string>());
    }
}