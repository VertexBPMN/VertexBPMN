#nullable enable
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.Extensions;
using VertexBPMN.Application.Messaging;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>
/// Extension module to register all engine service implementations in this project.
/// Chooses persistent or in-memory variants where both exist (persistent first).
/// </summary>
public static class EngineServiceModule
{
    /// <summary>
    /// Registers engine services into the DI container.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="useInMemory">If true, register in-memory implementations instead of persistent ones (for tests).</param>
    public static IServiceCollection AddVertexEngineServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Shared singleton/event sink
        services.AddScoped<IProcessMiningEventSink, ProcessMiningEventSink>();
        services.AddScoped<IRepositoryService, RepositoryService>();
        services.AddScoped<IRuntimeService, RuntimeService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<IAiDecisionService, XAiDecisionService>();
        services.AddScoped<IManagementService, ManagementService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IDecisionService, DecisionService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IProcessMigrationService, ProcessMigrationService>();
        services.AddScoped<ISemanticValidationService, SemanticValidationService>();
        services.AddScoped<ISimulationService, SimulationService>();
        services.AddScoped<IMcpAgentService, McpAgentService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddServiceTaskHandlers();
        services.AddHostedService<JobExecutorService>();
        return services;
    }
}
