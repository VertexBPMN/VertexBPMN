#nullable enable
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Contracts;
using VertexBPMN.EngineServices.Extensions;
using VertexBPMN.EngineServices.Messaging;

namespace VertexBPMN.EngineServices;

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
    public static IServiceCollection AddVertexEngineServices(this IServiceCollection services, bool useInMemory = false)
    {
        // Shared singleton/event sink
        services.AddSingleton<IProcessMiningEventSink, ProcessMiningEventSink>();

        if (useInMemory)
        {
            services.AddSingleton<IRepositoryService, InMemoryRepositoryService>(); // still needs repository behind but kept for completeness
            services.AddSingleton<IRuntimeService, InMemoryRuntimeService>();
            services.AddSingleton<ITaskService, InMemoryTaskService>();
            services.AddSingleton<IHistoryService, InMemoryHistoryService>();
            services.AddSingleton<IAiDecisionService, FakeAiDecisionService>();
            services.AddSingleton<IProcessInstanceStore, InMemoryProcessInstanceStore>();
            services.AddSingleton<IProcessMiningEventSink, InMemoryEventSink>();
            services.AddSingleton<IMessageDispatcher, InMemoryMessageDispatcher>();
        }
        else
        {
            services.AddScoped<IRepositoryService, RepositoryService>();
            services.AddScoped<IRuntimeService, RuntimeService>();
            services.AddScoped<ITaskService, TaskService>();
            services.AddScoped<IHistoryService, HistoryService>();
            services.AddScoped<IAiDecisionService, XAiDecisionService>();
            services.AddSingleton<IProcessInstanceStore, ProductionProcessInstanceStore>();
        }

        services.AddSingleton<IManagementService, ManagementService>();
        services.AddSingleton<IIdentityService, IdentityService>();
        services.AddSingleton<IDecisionService, DecisionService>();
        services.AddSingleton<IIncidentService, IncidentService>();
        services.AddSingleton<IProcessMigrationService, ProcessMigrationService>();
        services.AddSingleton<ISemanticValidationService, SemanticValidationService>();
        services.AddSingleton<ISimulationService, SimulationService>();
        services.AddSingleton<IMcpAgentService, McpAgentService>();
        services.AddSingleton<ITaskService, TaskService>();
        services.AddServiceTaskHandlers();
        services.AddHostedService<JobExecutorService>();
        return services;
    }
}
