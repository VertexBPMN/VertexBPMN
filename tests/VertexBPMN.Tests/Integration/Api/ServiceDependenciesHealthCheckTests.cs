using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VertexBPMN.Api;
using VertexBPMN.Api.Health;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine;
using VertexBPMN.Infrastructure;
using Xunit;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public class ServiceDependenciesHealthCheckTests
{
	private static ServiceProvider Create(params (Type serviceType, object? impl)[] overrides)
	{
		var sc = new ServiceCollection();

		// Use ConfigurationBuilder to create an IConfiguration instance
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
			{
				["Database:Provider"] = "InMemory",
				["Database:ConnectionString"] = "Filename=:memory:"
			})
			.Build();

		sc.AddApiServices(configuration);
		sc.AddAllEngineDbContexts(configuration);
		sc.AddRuntimePersistenceServices(configuration);
		sc.AddApplicationServices(configuration);
		sc.AddEngineServices(configuration);

		foreach (var (serviceType, implementation) in overrides)
		{
			sc.RemoveAll(serviceType);
			if (implementation is not null)
			{
				sc.AddSingleton(serviceType, implementation);
			}
		}
		return sc.BuildServiceProvider();
	}

	[Fact]
	public async Task AllServicesPresent_ReturnsHealthy()
	{
		await using var provider = Create();
		var hc = new ServiceDependenciesHealthCheck(provider);
		var ctx = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("service_deps", hc, null, [])
		};

		var result = await hc.CheckHealthAsync(ctx, TestContext.Current.CancellationToken);

		Assert.Equal(HealthStatus.Healthy, result.Status);
		Assert.Equal("All critical services resolved", result.Description);
	}

	[Fact]
	public async Task MissingService_ReturnsUnhealthy_WithMissingList()
	{
		// Omit IHistoryService
		await using var provider = Create((typeof(IHistoryService), null));
		var hc = new ServiceDependenciesHealthCheck(provider);
		var ctx = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("service_deps", hc, null, [])
		};

		var result = await hc.CheckHealthAsync(ctx, TestContext.Current.CancellationToken);

		Assert.Equal(HealthStatus.Unhealthy, result.Status);
		Assert.Contains("Missing", result.Description, StringComparison.Ordinal);
		Assert.Contains("missingServices", result.Data.Keys, StringComparer.Ordinal);
		var missing = Assert.IsType<List<string>>(result.Data["missingServices"]);
		Assert.Contains("IHistoryService", missing, StringComparer.Ordinal);
	}
}
