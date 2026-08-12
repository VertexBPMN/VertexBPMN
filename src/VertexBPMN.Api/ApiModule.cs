#nullable enable
using VertexBPMN.Api.Debug;
using VertexBPMN.Api.Migration;
using VertexBPMN.Api.ML;
using VertexBPMN.Api.Middleware;
using VertexBPMN.Api.Plugins;
using VertexBPMN.Api.Services;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api;

/// <summary>
/// Extension module to register all api service implementations in this project.
/// </summary>
public static class ApiModule
{
    /// <summary>
    /// Registers engine services into the DI container.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration"></param>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<Controllers.VisualDebuggerController>();

        //Production-Grade Features: Security, Caching, Resilience, Rate Limiting, Health Monitoring
        services.AddMemoryCache(); // Required for ProductionCachingService
        services.AddScoped<ICachingService, ProductionCachingService>();
        services.AddScoped<IResilienceService, ProductionResilienceService>();
        services.AddScoped<IRateLimitingService, ProductionRateLimitingService>();
        services.AddScoped<IHealthMonitoringService, ProductionHealthMonitoringService>();
        services.AddScoped<IPredictiveAnalyticsService, HistoricalPredictiveAnalyticsService>();
        services.AddScoped<ILiveProcessMigrationService, LiveProcessMigrationService>();
        services.AddSingleton<IVisualDebuggingService, VisualDebuggingService>();
        services.AddScoped<IVisualDebugStepService, PersistentVisualDebugStepService>();
        services.AddScoped<IProcessVisualizationService, PersistentProcessVisualizationService>();
        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddTransient<AuditLoggingMiddleware>();

        return services;
    }
}
