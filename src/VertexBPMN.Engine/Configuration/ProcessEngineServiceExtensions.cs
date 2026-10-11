using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Engine.Configuration;

/// <summary>
/// Extension methods for service collection registration.
/// Follows VertexBPMN's dependency injection patterns.
/// </summary>
public static class ProcessEngineServiceExtensions
{
	/// <summary>
	/// Registers a simple process engine with minimal dependencies
	/// </summary>
	public static IServiceCollection AddSimpleProcessEngine(this IServiceCollection services)
	{
		services.AddScoped<ProcessEngine>();
		services.AddScoped<IProcessEngine>(provider =>
			provider.GetRequiredService<ProcessEngine>());
		return services;
	}

	/// <summary>
	/// Registers a distributed process engine with full enterprise features
	/// </summary>
	public static IServiceCollection AddDistributedProcessEngine(this IServiceCollection services)
	{
		// Register the existing DistributedTokenEngine
		services.AddScoped<DistributedProcessEngine>();

		services.AddScoped<IDistributedProcessEngine>(provider =>
			provider.GetRequiredService<DistributedProcessEngine>());
		services.AddScoped<IProcessEngine>(provider =>
			provider.GetRequiredService<DistributedProcessEngine>());

		return services;
	}

	/// <summary>
	/// Registers the appropriate engine based on specified type
	/// </summary>
	public static IServiceCollection AddProcessEngine(this IServiceCollection services, ProcessEngineType engineType)
	{
		return engineType switch
		{
			ProcessEngineType.Simple => services.AddSimpleProcessEngine(),
			ProcessEngineType.Distributed => services.AddDistributedProcessEngine(),
			_ => throw new ArgumentException($"Unsupported engine type: {engineType}")
		};
	}

	/// <summary>
	/// Registers process engine from configuration with fallback to Simple
	/// </summary>
	public static IServiceCollection AddProcessEngineFromConfiguration(this IServiceCollection services)
	{
		services.AddScoped(provider =>
			ProcessEngineFactory.CreateFromConfiguration(provider));
		return services;
	}

	/// <summary>
	/// Registers both engine types and uses a factory for runtime selection
	/// </summary>
	public static IServiceCollection AddProcessEngineFactory(this IServiceCollection services)
	{
		services.AddSimpleProcessEngine();
		services.AddDistributedProcessEngine();

		services.AddScoped<Func<ProcessEngineType, IProcessEngine>>(provider =>
			engineType => ProcessEngineFactory.CreateEngine(engineType, provider));

		return services;
	}
}
