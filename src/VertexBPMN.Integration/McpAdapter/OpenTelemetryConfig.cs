#nullable enable
using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace VertexBPMN.McpAdapter;

/// <summary>
/// OpenTelemetry constants for the MCP Adapter.
/// </summary>

/// <summary>
/// OpenTelemetry setup for the MCP Adapter, aligned with the API's OpenTelemetryConfig style.
/// </summary>
internal static class OpenTelemetryConfig
{
	private static readonly ActivitySource ActivitySource = new(McpTelemetryConstants.ActivitySourceName);

	/// <summary>
	/// Adds tracing + metrics for the MCP Adapter with Jaeger + console exporters.
	/// Configuration keys (optional):
	///  Telemetry:Jaeger:Endpoint  - override Jaeger collector endpoint
	///  Build:Version               - service version
	///  DOTNET_ENVIRONMENT / ASPNETCORE_ENVIRONMENT - environment name
	/// </summary>
	public static IServiceCollection AddMcpAdapterTelemetry(this IServiceCollection services, IConfiguration? configuration = null)
	{
		var serviceVersion = configuration?["Build:Version"] ?? "dev";
		var environment = configuration?["DOTNET_ENVIRONMENT"]
						   ?? configuration?["ASPNETCORE_ENVIRONMENT"]
						   ?? "Development";

		services.AddSingleton(ActivitySource);

		services.AddOpenTelemetry()
			.ConfigureResource(rb => rb
				.AddService(
					serviceName: McpTelemetryConstants.ServiceName,
					serviceVersion: serviceVersion,
					serviceInstanceId: Environment.MachineName)
				.AddAttributes(new[]
				{
					new KeyValuePair<string, object>("service.namespace", McpTelemetryConstants.ServiceNamespace),
					new KeyValuePair<string, object>("deployment.environment", environment)
				}))
			.WithTracing(builder =>
			{
				builder
					.AddSource(McpTelemetryConstants.ActivitySourceName)
					.AddHttpClientInstrumentation(o => o.RecordException = true)
					// If the adapter hosts minimal HTTP endpoints, keep ASP.NET Core instrumentation:
					.AddAspNetCoreInstrumentation(o =>
					{
						o.RecordException = true;
						o.Filter = _ => true;
					})
					.AddConsoleExporter();
			})
			.WithMetrics(builder =>
			{
				builder
					.AddMeter(McpTelemetryConstants.MeterName)
					.AddRuntimeInstrumentation()
					//.AddProcessInstrumentation() // enable if package added
					.AddHttpClientInstrumentation()
					.AddAspNetCoreInstrumentation();
				// .AddPrometheusExporter();  // enable when Prometheus is desired
			});

		return services;
	}

	// Backward-compatible alias matching previous extension name used in Program.cs
	public static IServiceCollection AddVertexTelemetry(this IServiceCollection services, IConfiguration? configuration = null)
		=> services.AddMcpAdapterTelemetry(configuration);
}
