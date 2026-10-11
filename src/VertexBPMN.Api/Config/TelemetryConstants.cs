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
