using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;

public static class TelemetrySetup
{
    public static void AddVertexTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("VertexBPMN.McpAdapter"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("VertexBPMN.McpAdapter")
                .AddConsoleExporter()
            )
            .WithMetrics(builder => builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("VertexBPMN.McpAdapter"))
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddConsoleExporter()
            );
    }
}
