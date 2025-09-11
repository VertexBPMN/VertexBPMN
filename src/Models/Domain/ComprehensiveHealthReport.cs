using System;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VertexBPMN.Domain;

public class ComprehensiveHealthReport
{
    public string OverallStatus { get; set; } = string.Empty;
    public TimeSpan CheckDuration { get; set; }
    public DateTime Timestamp { get; set; }
    public HealthCheckResult DatabaseHealth { get; set; } = HealthCheckResult.Healthy();
    public HealthCheckResult MemoryHealth { get; set; } = HealthCheckResult.Healthy();
    public HealthCheckResult DiskSpaceHealth { get; set; } = HealthCheckResult.Healthy();
    public HealthCheckResult ExternalServicesHealth { get; set; } = HealthCheckResult.Healthy();
    public SystemInfo SystemInfo { get; set; } = new();
}