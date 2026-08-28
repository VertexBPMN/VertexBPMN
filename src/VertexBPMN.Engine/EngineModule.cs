#nullable enable
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Configuration;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Engine;

/// <summary>
/// Extension module to register all engine service implementations in this project.
/// Chooses persistent or in-memory variants where both exist (persistent first).
/// </summary>
public static class EngineModule
{
    /// <summary>
    /// Registers engine services into the DI container.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="useInMemory">If true, register in-memory implementations instead of persistent ones (for tests).</param>
    public static IServiceCollection AddEngineServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Shared singleton/event sink;
        services.AddScoped<IDmnEngine, DmnEngine>();
        services.AddScoped<IDmnParser, DmnParser>();
        services.AddScoped<ICmmnParser, CmmnParser>();
        services.AddScoped<IBpmnParser, BpmnParser>();
        services.AddScoped<IWorkerNodeManager, PersistentWorkerNodeManager>();
        services.AddScoped<ProcessEngine>();
        services.AddScoped<DistributedProcessEngine>();
        services.AddScoped<IDistributedProcessEngine, DistributedProcessEngine>();
        services.AddScoped<ICaseExecutionRuntime, PersistentCaseExecutionRuntime>();
        services.AddScoped<IProcessExecutionRuntime, PersistentProcessExecutionRuntime>();
        services.AddScoped<ISimulationService, DeterministicSimulationService>();
        services.AddSingleton<IProcessEngine>(provider =>
            ProcessEngineFactory.CreateFromConfiguration(provider));
        return services;
    }
}
