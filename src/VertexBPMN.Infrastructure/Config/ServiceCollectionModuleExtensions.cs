using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace VertexBPMN.Infrastructure.Config;

public static class ServiceCollectionModuleExtensions
{
    public static IServiceCollection AddWhen(this IServiceCollection services, bool condition, Action<IServiceCollection> add)
    {
        if (condition) add(services);
        return services;
    }

    public static OperationalMode ResolveOperationalMode(this IHostEnvironment env, IConfiguration config)
    {
        var configured = config.GetValue<string>("OperationalMode");
        var raw = configured ?? env.EnvironmentName;

        return raw.ToLowerInvariant() switch
        {
            "prod" or "production" => OperationalMode.Production,
            "stage" or "staging" => OperationalMode.Stage,
            "dev" or "development" => OperationalMode.Development,
            "test" or "unittest" => OperationalMode.Test,
            _ => OperationalMode.Development
        };
    }
}