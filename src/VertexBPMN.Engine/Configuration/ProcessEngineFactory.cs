using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Engine.Configuration;

/// <summary>
/// Factory for creating process engines based on requirements.
/// Follows VertexBPMN architectural patterns and dependency injection best practices.
/// </summary>
public static class ProcessEngineFactory
{
	/// <summary>
	/// Creates a process engine based on the specified type
	/// </summary>
	/// <param name="engineType">Type of engine to create</param>
	/// <param name="services">Service provider for dependency injection</param>
	/// <returns>Configured process engine</returns>
	/// <exception cref="ArgumentException">When unsupported engine type is specified</exception>
	/// <exception cref="InvalidOperationException">When required services are not registered</exception>
	public static IProcessEngine CreateEngine(ProcessEngineType engineType, IServiceProvider services)
	{
		return engineType switch
		{
			ProcessEngineType.Simple => CreateSimpleEngine(services),
			ProcessEngineType.Distributed => CreateDistributedEngine(services),
			_ => throw new ArgumentException($"Unsupported engine type: {engineType}", nameof(engineType))
		};
	}

	/// <summary>
	/// Creates a simple TokenEngine wrapped in an adapter
	/// </summary>
	private static ProcessEngine CreateSimpleEngine(IServiceProvider services)
	{
		var logger = services.GetService<ILogger<ProcessEngine>>();
		var serviceRegistry = services.GetService<IServiceTaskRegistry>();
		var bpmnParser = services.GetService<IBpmnParser>();
		var dmnParser = services.GetService<IDmnParser>();
		var dmnEngine = services.GetService<IDmnEngine>();

		var tokenEngine = new ProcessEngine(
			logger ?? throw new InvalidOperationException("ILogger<TokenEngine> not registered. Add logging services."),
			serviceRegistry ?? throw new InvalidOperationException("IServiceTaskRegistry not registered. Add service task registry."),
			bpmnParser: bpmnParser,
			dmnParser: dmnParser,
			dmnEngine: dmnEngine,
			cmmnParser: services.GetService<ICmmnParser>(),
			aiDecisionService: services.GetService<IAiDecisionService>()
		);

		return tokenEngine;
	}

	/// <summary>
	/// Creates a distributed engine with full enterprise features
	/// </summary>
	private static DistributedProcessEngine CreateDistributedEngine(IServiceProvider services)
	{
		return services.GetRequiredService<DistributedProcessEngine>();
	}

	/// <summary>
	/// Creates an engine based on configuration
	/// </summary>
	/// <param name="services">Service provider</param>
	/// <param name="configKey">Configuration key (default: "ProcessEngine:Type")</param>
	/// <returns>Configured process engine</returns>
	/// <exception cref="InvalidOperationException">When configuration is invalid or missing</exception>
	public static IProcessEngine CreateFromConfiguration(IServiceProvider services, string configKey = "ProcessEngine:Type")
	{
		var configuration = services.GetRequiredService<IConfiguration>();
		var engineTypeString = configuration[configKey] ?? "Simple";

		if (Enum.TryParse<ProcessEngineType>(engineTypeString, true, out var engineType))
		{
			return CreateEngine(engineType, services);
		}

		throw new InvalidOperationException(
			$"Invalid engine type in configuration '{configKey}': {engineTypeString}. " +
			$"Valid values: {string.Join(", ", Enum.GetNames<ProcessEngineType>())}");
	}

	/// <summary>
	/// Determines the best engine type based on functional requirements.
	/// Follows VertexBPMN's architectural decision framework.
	/// </summary>
	/// <param name="requiresCmmn">Whether CMMN case management is needed</param>
	/// <param name="requiresDmn">Whether DMN decision support is needed</param>
	/// <param name="requiresDistribution">Whether distributed execution is needed</param>
	/// <param name="requiresScalability">Whether high scalability is needed</param>
	/// <param name="requiresAiFeatures">Whether AI-enhanced features are needed</param>
	/// <returns>Recommended engine type</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163", Justification = "Public compatibility flags describe capabilities common to both engine choices; only distribution and scalability select the engine kind.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060", Justification = "Keep existing public named arguments; CMMN, DMN and AI do not distinguish the two engine kinds.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar Code Smell", "S1172", Justification = "Preserve public capability flags without inventing different engine selection for shared capabilities.")]
	public static ProcessEngineType GetRecommendedEngineType(
		bool requiresCmmn = false,
		bool requiresDmn = false,
		bool requiresDistribution = false,
		bool requiresScalability = false,
		bool requiresAiFeatures = false)
	{
		// CMMN and DMN are supported in both engines. Distribution and scalability require the cluster engine.
		if (requiresDistribution || requiresScalability)
		{
			return ProcessEngineType.Distributed;
		}

		return ProcessEngineType.Simple;
	}

	/// <summary>
	/// Validates that an engine supports the required features
	/// </summary>
	/// <param name="engine">Engine to validate</param>
	/// <param name="requiresCmmn">Whether CMMN support is required</param>
	/// <param name="requiresDistribution">Whether distribution support is required</param>
	/// <returns>Validation result with recommendations</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163", Justification = "IProcessEngine already declares CMMN execution; the public requiresCmmn named argument is retained for compatibility.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060", Justification = "CMMN is part of IProcessEngine; retaining its existing public requirement flag avoids breaking callers.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar Code Smell", "S1172", Justification = "CMMN is a shared contract capability, not a marker for distributed execution.")]
	public static EngineValidationResult ValidateEngineCapabilities(
		IProcessEngine engine,
		bool requiresCmmn = false,
		bool requiresDistribution = false)
	{
		ArgumentNullException.ThrowIfNull(engine);
		var isDistributed = engine is IDistributedProcessEngine;
		var issues = new List<string>();
		var recommendations = new List<string>();

		if (requiresDistribution && !isDistributed)
		{
			issues.Add("Distributed processing required but engine does not implement IDistributedProcessEngine");
			recommendations.Add("Switch to UnifiedDistributedProcessEngine or ProcessEngineType.Distributed");
		}

		return new EngineValidationResult(
			IsValid: issues.Count == 0,
			Issues: issues,
			Recommendations: recommendations,
			EngineType: isDistributed ? ProcessEngineType.Distributed : ProcessEngineType.Simple
		);
	}
}
