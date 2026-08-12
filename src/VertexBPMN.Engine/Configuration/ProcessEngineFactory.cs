using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Engine.Configuration;

/// <summary>
/// Engine types supported by the factory
/// </summary>
public enum ProcessEngineType
{
    /// <summary>
    /// Simple, single-node engine with in-memory BPMN, CMMN and DMN execution
    /// - ✅ BPMN execution
    /// - ✅ CMMN support
    /// - ✅ DMN registry
    /// - ❌ Distribution
    /// - ❌ Worker management
    /// </summary>
    Simple,
    
    /// <summary>
    /// Distributed, enterprise-grade engine with the same domain features plus server capabilities
    /// - ✅ BPMN execution
    /// - ✅ CMMN case management
    /// - ✅ DMN decision support
    /// - ✅ Distributed processing
    /// - ✅ Worker management
    /// - ✅ AI-enhanced features
    /// </summary>
    Distributed
}

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
    private static IProcessEngine CreateSimpleEngine(IServiceProvider services)
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
    private static IDistributedProcessEngine CreateDistributedEngine(IServiceProvider services)
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
    public static EngineValidationResult ValidateEngineCapabilities(
        IProcessEngine engine,
        bool requiresCmmn = false,
        bool requiresDistribution = false)
    {
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

/// <summary>
/// Result of engine capability validation
/// </summary>
/// <param name="IsValid">Whether the engine meets all requirements</param>
/// <param name="Issues">List of capability issues</param>
/// <param name="Recommendations">List of recommendations to resolve issues</param>
/// <param name="EngineType">Detected engine type</param>
public record EngineValidationResult(
    bool IsValid,
    List<string> Issues,
    List<string> Recommendations,
    ProcessEngineType EngineType
);

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