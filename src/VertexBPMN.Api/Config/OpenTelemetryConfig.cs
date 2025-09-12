#nullable enable
using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
    /// <summary>
    /// Default Jaeger HTTP collector endpoint (Thrift). Change via config key Telemetry:Jaeger:Endpoint.
    /// </summary>
    public const string DefaultJaegerEndpoint = "http://localhost:14268/api/traces";
}

/// <summary>
/// Provides extension methods to configure OpenTelemetry (tracing + metrics) for the VertexBPMN API.
/// </summary>
public static class OpenTelemetryConfig
{
    private static readonly ActivitySource ActivitySource = new(TelemetryConstants.ActivitySourceName);

    /// <summary>
    /// Adds and configures OpenTelemetry tracing and metrics for the VertexBPMN service.
    /// The Jaeger endpoint can be overridden via configuration key: <c>Telemetry:Jaeger:Endpoint</c>.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddVertexBPMNTelemetry(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var jaegerEndpoint = configuration?["Telemetry:Jaeger:Endpoint"]
                              ?? TelemetryConstants.DefaultJaegerEndpoint;
        var serviceVersion = configuration?["Build:Version"] ?? "dev";
        var environment = configuration?["DOTNET_ENVIRONMENT"]
                           ?? configuration?["ASPNETCORE_ENVIRONMENT"]
                           ?? "Development";

        services.AddSingleton(ActivitySource); // in case consumers want to inject it

        services.AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(
                    serviceName: TelemetryConstants.ServiceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new[]
                {
                                        new KeyValuePair<string, object>("service.namespace", TelemetryConstants.ServiceNamespace),
                                        new KeyValuePair<string, object>("deployment.environment", environment)
                }))
            .WithTracing(builder =>
            {
                builder
                    .AddSource(TelemetryConstants.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.Filter = _ => true;
                    })
                    .AddHttpClientInstrumentation(o =>
                    {
                        o.RecordException = true;
                    })
                    .AddConsoleExporter();

                                    builder.AddJaegerExporter(o =>
                                    {
                                        if (Uri.TryCreate(jaegerEndpoint, UriKind.Absolute, out var uri))
                                        {
                                            o.Endpoint = uri;
                                        }
                                    });

            })
            .WithMetrics(builder =>
            {
                builder
                    .AddMeter(TelemetryConstants.MeterName)
                    .AddRuntimeInstrumentation()
                    //.AddProcessInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                    //.AddPrometheusExporter();
            });

        return services;
    }
}