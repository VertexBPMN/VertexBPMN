using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using SendGrid;
using VertexBPMN.Application.Fakes;
using VertexBPMN.Application.Configuration;
using VertexBPMN.Application.Handlers;
using VertexBPMN.Domain.Interfaces;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace VertexBPMN.Application.Extensions;

public static class ServiceTaskRegistryExtensions
{
    public static IServiceCollection AddServiceTaskHandlers(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Registriere alle Handler und Abhängigkeiten
        RegisterCoreDependencies(services);
        RegisterHandlers(services);

        // ✅ KORREKTE Registry-Registrierung
        services.AddSingleton<IServiceTaskRegistry>(provider =>
        {
            var registry = new ServiceTaskRegistry();

            // ✅ Handler werden EINMAL aus dem DI Container geholt (respektiert Singleton)
            registry.Register("semanticKernelServiceTask", provider.GetRequiredService<SemanticKernelServiceTaskHandler>());
            registry.Register("calculateScore", provider.GetRequiredService<CalculateScoreServiceTaskHandler>());
            registry.Register("cancelApplication", provider.GetRequiredService<CancelApplicationServiceTaskHandler>());
            registry.Register("issuePolicy", provider.GetRequiredService<IssuePolicyServiceTaskHandler>());
            registry.Register("rejectPolicy", provider.GetRequiredService<RejectPolicyServiceTaskHandler>());
            registry.Register("io.camunda:sendgrid:1", provider.GetRequiredService<SendGridServiceTaskHandler>());
            registry.Register("informCustomerSuccessfulCancelation", provider.GetRequiredService<InformCustomerSuccessfulCancelationHandler>());
            registry.Register("reportFraud", provider.GetRequiredService<ReportFraudHandler>());
            registry.Register("informOperationsSuccessfulCancelation", provider.GetRequiredService<InformOperationsSuccessfulCancelationHandler>());
            registry.Register("mcpServiceTask", provider.GetRequiredService<McpServiceTaskHandler>());

            // ✅ NEW: Universal AI Service Task Handler
            registry.Register("aiServiceTask", provider.GetRequiredService<AIServiceTaskHandler>());
            registry.Register("ai:universal", provider.GetRequiredService<AIServiceTaskHandler>());
            registry.Register("ai:smart", provider.GetRequiredService<AIServiceTaskHandler>());

            // ✅ Specialized AI Service Task Handlers
            registry.Register("ai:openai", provider.GetRequiredService<OpenAiServiceTaskHandler>());
            registry.Register("ai:openai:gpt-4", provider.GetRequiredService<OpenAiServiceTaskHandler>());
            registry.Register("ai:openai:gpt-3.5-turbo", provider.GetRequiredService<OpenAiServiceTaskHandler>());
            registry.Register("openAiServiceTask", provider.GetRequiredService<OpenAiServiceTaskHandler>());

            registry.Register("ai:anthropic", provider.GetRequiredService<AnthropicServiceTaskHandler>());
            registry.Register("ai:anthropic:claude-3", provider.GetRequiredService<AnthropicServiceTaskHandler>());
            registry.Register("ai:claude", provider.GetRequiredService<AnthropicServiceTaskHandler>());
            registry.Register("anthropicServiceTask", provider.GetRequiredService<AnthropicServiceTaskHandler>());

            registry.Register("ai:google", provider.GetRequiredService<GeminiServiceTaskHandler>());
            registry.Register("ai:gemini", provider.GetRequiredService<GeminiServiceTaskHandler>());
            registry.Register("ai:gemini:pro", provider.GetRequiredService<GeminiServiceTaskHandler>());
            registry.Register("geminiServiceTask", provider.GetRequiredService<GeminiServiceTaskHandler>());

            registry.Register("contextEnrichment", provider.GetRequiredService<ContextEnrichmentServiceTaskHandler>());
            registry.Register("dataEnrichment", provider.GetRequiredService<ContextEnrichmentServiceTaskHandler>());
            registry.Register("externalDataFetch", provider.GetRequiredService<ContextEnrichmentServiceTaskHandler>());

            registry.Register("ai:generic", provider.GetRequiredService<GenericAiServiceTaskHandler>());
            registry.Register("genericAi", provider.GetRequiredService<GenericAiServiceTaskHandler>());

            // Additional AI provider mappings
            registry.Register("ai:cohere", provider.GetRequiredService<GenericAiServiceTaskHandler>());
            registry.Register("ai:huggingface", provider.GetRequiredService<GenericAiServiceTaskHandler>());
            registry.Register("ai:ollama", provider.GetRequiredService<GenericAiServiceTaskHandler>());
            registry.Register("ai:local", provider.GetRequiredService<GenericAiServiceTaskHandler>());
            registry.Register("ai:custom", provider.GetRequiredService<GenericAiServiceTaskHandler>());

            var options = provider.GetService<DependencyOptions>() ?? new DependencyOptions();
            if (!options.ServiceTasks.Enabled || !options.Ai.Enabled)
            {
                foreach (var implementation in new[] { "aiServiceTask", "ai:universal", "ai:smart", "ai:openai", "openAiServiceTask", "ai:anthropic", "ai:claude", "anthropicServiceTask", "ai:google", "ai:gemini", "ai:gemini:pro", "geminiServiceTask", "ai:generic", "genericAi", "ai:cohere", "ai:huggingface", "ai:ollama", "ai:local", "ai:custom" })
                    registry.Remove(implementation);
            }

            foreach (var implementation in options.ServiceTasks.Disabled)
                registry.Remove(implementation);

            foreach (var mapping in options.ServiceTasks.Mappings)
            {
                if (!handlers.TryGetValue(mapping.Value, out var handlerFactory))
                    throw new InvalidOperationException($"Unknown service task handler '{mapping.Value}' configured for '{mapping.Key}'.");
                registry.Register(mapping.Key, handlerFactory(provider));
            }

            return registry;
        });

        return services;
    }

    private static readonly IReadOnlyDictionary<string, Func<IServiceProvider, IServiceTaskHandler>> handlers =
        new Dictionary<string, Func<IServiceProvider, IServiceTaskHandler>>(StringComparer.OrdinalIgnoreCase)
        {
            ["AIServiceTaskHandler"] = p => p.GetRequiredService<AIServiceTaskHandler>(),
            ["OpenAiServiceTaskHandler"] = p => p.GetRequiredService<OpenAiServiceTaskHandler>(),
            ["AnthropicServiceTaskHandler"] = p => p.GetRequiredService<AnthropicServiceTaskHandler>(),
            ["GeminiServiceTaskHandler"] = p => p.GetRequiredService<GeminiServiceTaskHandler>(),
            ["GenericAiServiceTaskHandler"] = p => p.GetRequiredService<GenericAiServiceTaskHandler>(),
            ["ContextEnrichmentServiceTaskHandler"] = p => p.GetRequiredService<ContextEnrichmentServiceTaskHandler>(),
            ["McpServiceTaskHandler"] = p => p.GetRequiredService<McpServiceTaskHandler>(),
            ["CalculateScoreServiceTaskHandler"] = p => p.GetRequiredService<CalculateScoreServiceTaskHandler>(),
            ["CancelApplicationServiceTaskHandler"] = p => p.GetRequiredService<CancelApplicationServiceTaskHandler>(),
            ["IssuePolicyServiceTaskHandler"] = p => p.GetRequiredService<IssuePolicyServiceTaskHandler>(),
            ["RejectPolicyServiceTaskHandler"] = p => p.GetRequiredService<RejectPolicyServiceTaskHandler>(),
            ["SendGridServiceTaskHandler"] = p => p.GetRequiredService<SendGridServiceTaskHandler>()
        };

    private static void RegisterCoreDependencies(IServiceCollection services)
    {
        // Registriere Kernabhängigkeiten
        services.AddSingleton<IKernelFactory, CachingKernelFactory>();
        services.AddSingleton<ISendGridClient, FakeSendGridClient>();
        
        // ✅ NEW: AI-specific dependencies
        services.AddSingleton<HttpClient>();
        
        // Simple TracerProvider registration (if not already present)
        services.TryAddSingleton<TracerProvider>(provider => 
            Sdk.CreateTracerProviderBuilder()
                .AddSource("VertexBPMN")
                .Build() ?? throw new InvalidOperationException("Failed to create TracerProvider"));
    }

    private static void RegisterHandlers(IServiceCollection services)
    {
        // ✅ Handler bleiben Singleton (korrekt für stateless workers)
        services.AddSingleton<CalculateScoreServiceTaskHandler>();
        services.AddSingleton<CancelApplicationServiceTaskHandler>();
        services.AddSingleton<IssuePolicyServiceTaskHandler>();
        services.AddSingleton<RejectPolicyServiceTaskHandler>();
        services.AddSingleton<SendGridServiceTaskHandler>();
        services.AddSingleton<ReportFraudHandler>();
        services.AddSingleton<SemanticKernelServiceTaskHandler>();
        services.AddSingleton<InformCustomerSuccessfulCancelationHandler>();
        services.AddSingleton<InformOperationsSuccessfulCancelationHandler>();
        services.AddSingleton<McpServiceTaskHandler>();

        // ✅ NEW: Universal AI Service Task Handler (Primary)
        services.AddSingleton<AIServiceTaskHandler>();

        // ✅ Specialized AI Service Task Handlers (Secondary)
        services.AddSingleton<OpenAiServiceTaskHandler>();
        services.AddSingleton<AnthropicServiceTaskHandler>();
        services.AddSingleton<GeminiServiceTaskHandler>();
        services.AddSingleton<ContextEnrichmentServiceTaskHandler>();
        services.AddSingleton<GenericAiServiceTaskHandler>();
    }
}