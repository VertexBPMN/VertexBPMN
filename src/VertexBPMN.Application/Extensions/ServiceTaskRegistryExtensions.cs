using Microsoft.Extensions.DependencyInjection;
using SendGrid;
using VertexBPMN.Application.Fakes;
using VertexBPMN.Application.Handlers;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Extensions;

public static class ServiceTaskRegistryExtensions
{
    public static IServiceCollection AddServiceTaskHandlers(this IServiceCollection services)
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

            return registry;
        });

        return services;
    }

    private static void RegisterCoreDependencies(IServiceCollection services)
    {
        // Registriere Kernabhängigkeiten
        services.AddSingleton<IKernelFactory, CachingKernelFactory>();
        services.AddSingleton<ISendGridClient, FakeSendGridClient>();
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
    }
}