using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Runtime;
using OpenTelemetry.Instrumentation.Process;

public static class TelemetrySetup
{
    public static void AddVertexTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetryTracing(builder => builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("VertexBPMN.McpAdapter"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("VertexBPMN.McpAdapter")
            .AddConsoleExporter()
        );
        services.AddOpenTelemetryMetrics(builder => builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("VertexBPMN.McpAdapter"))
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddConsoleExporter()
        );
    }
}
