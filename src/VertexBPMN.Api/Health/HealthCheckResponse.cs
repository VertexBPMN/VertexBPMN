using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VertexBPMN.Api.Health;

/// <summary>HTTP-safe health data. Exceptions and their reflection/stack data stay server-side.</summary>
public sealed record HealthCheckResponse(HealthStatus Status, string? Description, IReadOnlyDictionary<string, object> Data)
{
    public static HealthCheckResponse From(HealthCheckResult result) => new(result.Status, result.Description, result.Data);
}
