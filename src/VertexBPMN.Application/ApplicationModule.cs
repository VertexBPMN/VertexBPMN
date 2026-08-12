#nullable enable
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VertexBPMN.Application.Extensions;
using VertexBPMN.Application.Configuration;
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
        var dependencies = new DependencyOptions();
        configuration.GetSection("Dependencies").Bind(dependencies);
        services.AddSingleton(dependencies);
        services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
        services.AddScoped<ProcessMiningEventSink>();
        services.AddHttpClient("webhooks", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IProcessMiningEventSink>(sp => new Messaging.WebhookEventSink(
            sp.GetRequiredService<ProcessMiningEventSink>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            configuration,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Messaging.WebhookEventSink>>()));

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
        if (dependencies.Mcp.Enabled && dependencies.Interfaces.McpAgentService)
            services.AddHttpClient<IMcpAgentService, McpAgentService>();
        if (dependencies.Interfaces.LoadBalancing)
            services.AddScoped<ILoadBalancingService, LoadBalancingService>();


        if (dependencies.Interfaces.AiDecisionService)
            services.AddSingleton<IAiDecisionService, FakeAiDecisionService>();
        services.AddSingleton<ISemanticValidationService, SemanticValidationService>();
        services.AddSingleton<IIdentityService, IdentityService>();
        services.AddSingleton<IHostedService, JobExecutorService>();
        services.AddServiceTaskHandlers(configuration);
        return services;
    }
}
