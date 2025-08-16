using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SendGrid;
using VertexBPMN.Core.Fakes;
using VertexBPMN.Core.Handlers;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Extensions;

public static class ServiceTaskRegistryExtensions
{
    public static IServiceCollection AddServiceTaskHandlers(this IServiceCollection services)
    {
        // Registriere die ServiceTaskRegistry
        //services.AddSingleton<ServiceTaskRegistry>();

        // Registriere alle Handler und Abhängigkeiten
        RegisterCoreDependencies(services);
        RegisterHandlers(services);

        // Füge die Handler zur Registry hinzu
        services.AddSingleton(provider =>
        {
            var registry = new ServiceTaskRegistry();

            // Verwende Lazy Loading für die Registrierung
            var handlers = new Dictionary<string, Func<IServiceTaskHandler>>
            {
                { "semanticKernelServiceTask", () => provider.GetRequiredService<SemanticKernelServiceTaskHandler>() },
                { "calculateScore", () => provider.GetRequiredService<CalculateScoreServiceTaskHandler>() },
                { "cancelApplication", () => provider.GetRequiredService<CancelApplicationServiceTaskHandler>() },
                { "issuePolicy", () => provider.GetRequiredService<IssuePolicyServiceTaskHandler>() },
                { "rejectPolicy", () => provider.GetRequiredService<RejectPolicyServiceTaskHandler>() },
                { "io.camunda:sendgrid:1", () => provider.GetRequiredService<SendGridServiceTaskHandler>() },
                { "informCustomerSuccessfulCancelation", () => provider.GetRequiredService<InformCustomerSuccessfulCancelationHandler>() },
                { "reportFraud", () => provider.GetRequiredService<ReportFraudHandler>() },
                { "informOperationsSuccessfulCancelation", () => provider.GetRequiredService<InformOperationsSuccessfulCancelationHandler>() }
            };

            foreach (var (key, handlerFactory) in handlers)
            {
                registry.Register(key, new Lazy<IServiceTaskHandler>(handlerFactory).Value);
            }

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
        // Registriere alle Handler als Singleton
        services.AddSingleton<CalculateScoreServiceTaskHandler>();
        services.AddSingleton<CancelApplicationServiceTaskHandler>();
        services.AddSingleton<IssuePolicyServiceTaskHandler>();
        services.AddSingleton<RejectPolicyServiceTaskHandler>();
        services.AddSingleton<SendGridServiceTaskHandler>();
        services.AddSingleton<ReportFraudHandler>();
        services.AddSingleton<SemanticKernelServiceTaskHandler>();
        services.AddSingleton<InformCustomerSuccessfulCancelationHandler>();
        services.AddSingleton<InformOperationsSuccessfulCancelationHandler>();
    }
}