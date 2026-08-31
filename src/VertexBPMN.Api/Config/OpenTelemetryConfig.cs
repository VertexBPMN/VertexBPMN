#nullable enable
using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using VertexBPMN.Infrastructure.Operational;


namespace VertexBPMN.Api.Config;

/// <summary>
/// Constants used for OpenTelemetry setup within VertexBPMN.
/// </summary>
internal static class TelemetryConstants
{
    public const string ServiceName = "VertexBPMN";
    public const string ServiceNamespace = "vertex.bpmn";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName + ".metrics";
}

/// <summary>
/// Provides extension methods to configure OpenTelemetry (tracing + metrics) for the VertexBPMN API.
/// </summary>
public static class OpenTelemetryConfig
{
    private static readonly ActivitySource ActivitySource = new(TelemetryConstants.ActivitySourceName);

    /// <summary>
    /// Adds VertexBPMN-specific activity sources and meters to the shared Aspire telemetry pipeline.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddVertexBPMNTelemetry(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton(ActivitySource);

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .AddSource(TelemetryConstants.ActivitySourceName)
                    .AddSource(RuntimeTelemetry.ActivitySourceName);

                if (configuration?.GetValue("Telemetry:ConsoleExporter", false) == true)
                    builder.AddConsoleExporter();
            })
            .WithMetrics(builder =>
            {
                builder
                    .AddMeter(TelemetryConstants.MeterName)
                    .AddMeter(RuntimeTelemetry.MeterName);
            });

        return services;
    }

    public static WebApplicationBuilder AddVertexBPMNTelemetry(this WebApplicationBuilder builder)
    {
        builder.Services.AddVertexBPMNTelemetry(builder.Configuration);
        return builder;
    }
}
