#nullable enable
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VertexBPMN.Application.Extensions;
using VertexBPMN.Domain.Interfaces;
namespace VertexBPMN.Application;

/// <summary>
/// Extension module to register all engine service implementations in this project.
/// Chooses persistent or in-memory variants where both exist (persistent first).
/// </summary>
public static class ApplicationModule
{
    /// <summary>
    /// Registers engine services into the DI container.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="useInMemory">If true, register in-memory implementations instead of persistent ones (for tests).</param>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Shared singleton/event sink
        services.AddScoped<IProcessMiningEventSink, ProcessMiningEventSink>();

        services.AddScoped<IRepositoryService, RepositoryService>();
        services.AddScoped<IRuntimeService, RuntimeService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<IManagementService, ManagementService>();
        services.AddScoped<IDecisionService, DecisionService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<ISimulationService, SimulationService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IProcessMigrationService, ProcessMigrationService>();
        //services.AddHttpClient<IAiDecisionService, XAiDecisionService>();
        services.AddHttpClient<IMcpAgentService, McpAgentService>();
        services.AddScoped<ILoadBalancingService, LoadBalancingService>();


        services.AddSingleton<IAiDecisionService, FakeAiDecisionService>();
        services.AddSingleton<ISemanticValidationService, SemanticValidationService>();
        services.AddSingleton<IIdentityService, IdentityService>();
        services.AddSingleton<IHostedService, JobExecutorService>();
        services.AddServiceTaskHandlers();
        return services;
    }
}
