using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
namespace VertexBPMN.Core.Infrastructure;

public static class OpenTelemetryConfig
{
    public static IServiceCollection AddVertexBPMNTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetryTracing(builder =>
        {
            builder
                .AddSource("VertexBPMN")
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("VertexBPMN"))
                .AddHttpClientInstrumentation()
                .AddConsoleExporter() // Für Debugging, ersetze durch Jaeger/Zipkin
                .AddJaegerExporter(o =>
                {
                    o.Endpoint = new Uri("http://localhost:14268/api/traces");
                });
        });
        return services;
    }
}