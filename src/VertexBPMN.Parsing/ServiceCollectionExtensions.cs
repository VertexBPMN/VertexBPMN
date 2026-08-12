using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Parsing.ShadowMode;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Parsing;

/// <summary>
/// Phase 9: Dependency injection extensions for BPMN parsing services.
/// Registers unified parser as default while maintaining legacy compatibility.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds VertexBPMN parsing services to the DI container.
    /// Uses unified parser as default implementation.
    /// </summary>
    public static IServiceCollection AddVertexBpmnParsing(this IServiceCollection services)
    {
        // Register unified parser as the primary implementation
        services.AddSingleton<IBpmnParser>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<BpmnParser>>();
            var options = new BpmnParserOptions
            {
                BuildRuntimeProjection = true,
                NormalizeVendorExtensions = true,
                EnableAdvancedValidation = true,
                EnableLogging = logger != null,
                Logger = logger
            };
            return new BpmnParser(options);
        });
        
        // Register shadow mode facade for backward compatibility (with deprecation warning)
        services.AddSingleton<LegacyEngineParserFacade>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<LegacyEngineParserFacade>>();
            return new LegacyEngineParserFacade(logger);
        });
        
        // Register comparison utilities for migration validation
        services.AddSingleton<EngineParserComparator>();
        
        return services;
    }
    
    /// <summary>
    /// Adds VertexBPMN parsing services with custom options.
    /// </summary>
    public static IServiceCollection AddVertexBpmnParsing(this IServiceCollection services, 
        Action<BpmnParserOptions> configureOptions)
    {
        services.AddSingleton<IBpmnParser>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<BpmnParser>>();
            var options = new BpmnParserOptions();
            configureOptions(options);
            
            // Ensure logging is enabled if logger is available
            if (logger != null && !options.EnableLogging)
            {
                options.EnableLogging = true; 
                options.Logger = logger;
            }
            
            return new BpmnParser(options);
        });
        
        // Still register shadow facade for compatibility
        services.AddSingleton<LegacyEngineParserFacade>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<LegacyEngineParserFacade>>();
            return new LegacyEngineParserFacade(logger);
        });
        
        services.AddSingleton<EngineParserComparator>();
        
        return services;
    }
}