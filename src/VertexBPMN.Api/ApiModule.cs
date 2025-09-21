#nullable enable
using VertexBPMN.Api.Debug;
using VertexBPMN.Api.Hubs;
using VertexBPMN.Api.Migration;
using VertexBPMN.Api.ML;
using VertexBPMN.Api.Plugins;
using VertexBPMN.Api.Services;
using VertexBPMN.Application;
using VertexBPMN.Application.Extensions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence.Services;

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
        services.AddScoped<IPredictiveAnalyticsService, MLPredictiveAnalyticsService>();
        services.AddScoped<ILiveProcessMigrationService, LiveProcessMigrationService>();
        services.AddScoped<IVisualDebuggingService, VisualDebuggingService>();
        services.AddSingleton<IPluginManager, PluginManager>();

        return services;
    }
}
