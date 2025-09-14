using Microsoft.Extensions.Diagnostics.HealthChecks;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Production-Grade Health Monitoring Service
/// Olympic-level feature: Production-Grade Features - Health Monitoring
/// </summary>
public interface IHealthMonitoringService
{
    Task<HealthCheckResult> CheckDatabaseHealthAsync();
    Task<HealthCheckResult> CheckMemoryHealthAsync();
    Task<HealthCheckResult> CheckDiskSpaceHealthAsync();
    Task<HealthCheckResult> CheckExternalServicesHealthAsync();
    Task<ComprehensiveHealthReport> GetComprehensiveHealthReportAsync();
    Task<SystemMetrics> GetSystemMetricsAsync();
}