using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Api.Services;

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

public class ProductionHealthMonitoringService : IHealthMonitoringService
{
    private readonly ILogger<ProductionHealthMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;

    public ProductionHealthMonitoringService(
        ILogger<ProductionHealthMonitoringService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        try
        {
            // Initialize performance counters (Windows only)
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize performance counters");
        }
    }

    public async Task<HealthCheckResult> CheckDatabaseHealthAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            // Check if we can get database context services
            var dbContextServices = scope.ServiceProvider.GetServices<DbContext>();
            var healthyDatabases = new List<string>();
            var unhealthyDatabases = new List<string>();

            foreach (var dbContext in dbContextServices)
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    await dbContext.Database.CanConnectAsync();
                    stopwatch.Stop();
                    
                    var dbName = dbContext.GetType().Name;
                    healthyDatabases.Add($"{dbName} ({stopwatch.ElapsedMilliseconds}ms)");
                }
                catch (Exception ex)
                {
                    var dbName = dbContext.GetType().Name;
                    unhealthyDatabases.Add($"{dbName}: {ex.Message}");
                }
            }

            if (unhealthyDatabases.Any())
            {
                return HealthCheckResult.Unhealthy(
                    $"Database issues: {string.Join(", ", unhealthyDatabases)}",
                    data: new Dictionary<string, object>
                    {
                        ["healthy_databases"] = healthyDatabases,
                        ["unhealthy_databases"] = unhealthyDatabases
                    });
            }

            return HealthCheckResult.Healthy(
                $"All databases healthy: {string.Join(", ", healthyDatabases)}",
                data: new Dictionary<string, object>
                {
                    ["healthy_databases"] = healthyDatabases
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }

    public async Task<HealthCheckResult> CheckMemoryHealthAsync()
    {
        await Task.Delay(1); // Make async
        
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / 1024 / 1024;
            var privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
            
            // Get system memory info
            var gc = GC.GetTotalMemory(false) / 1024 / 1024;
            
            var data = new Dictionary<string, object>
            {
                ["working_set_mb"] = workingSetMB,
                ["private_memory_mb"] = privateMemoryMB,
                ["gc_memory_mb"] = gc,
                ["gen0_collections"] = GC.CollectionCount(0),
                ["gen1_collections"] = GC.CollectionCount(1),
                ["gen2_collections"] = GC.CollectionCount(2)
            };

            // Check thresholds
            if (workingSetMB > 1000) // Over 1GB working set
            {
                return HealthCheckResult.Degraded(
                    $"High memory usage: {workingSetMB}MB working set", data: data);
            }

            if (workingSetMB > 2000) // Over 2GB working set
            {
                return HealthCheckResult.Unhealthy(
                    $"Critical memory usage: {workingSetMB}MB working set", data: data);
            }

            return HealthCheckResult.Healthy(
                $"Memory healthy: {workingSetMB}MB working set", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory health check failed");
            return HealthCheckResult.Unhealthy("Memory health check failed", ex);
        }
    }

    public async Task<HealthCheckResult> CheckDiskSpaceHealthAsync()
    {
        await Task.Delay(1); // Make async
        
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            var driveInfos = new List<object>();
            var criticalDrives = new List<string>();
            var lowSpaceDrives = new List<string>();

            foreach (var drive in drives)
            {
                var totalGB = drive.TotalSize / 1024 / 1024 / 1024;
                var freeGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
                var usedPercentage = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100;

                driveInfos.Add(new
                {
                    name = drive.Name,
                    total_gb = totalGB,
                    free_gb = freeGB,
                    used_percentage = Math.Round(usedPercentage, 1)
                });

                if (usedPercentage > 95)
                {
                    criticalDrives.Add($"{drive.Name} ({usedPercentage:F1}% used)");
                }
                else if (usedPercentage > 85)
                {
                    lowSpaceDrives.Add($"{drive.Name} ({usedPercentage:F1}% used)");
                }
            }

            var data = new Dictionary<string, object>
            {
                ["drives"] = driveInfos
            };

            if (criticalDrives.Any())
            {
                return HealthCheckResult.Unhealthy(
                    $"Critical disk space: {string.Join(", ", criticalDrives)}", data: data);
            }

            if (lowSpaceDrives.Any())
            {
                return HealthCheckResult.Degraded(
                    $"Low disk space: {string.Join(", ", lowSpaceDrives)}", data: data);
            }

            return HealthCheckResult.Healthy(
                $"Disk space healthy across {drives.Count} drives", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disk space health check failed");
            return HealthCheckResult.Unhealthy("Disk space health check failed", ex);
        }
    }

    public async Task<HealthCheckResult> CheckExternalServicesHealthAsync()
    {
        var externalServices = new List<ExternalServiceCheck>
        {
            new("Google DNS", "8.8.8.8"),
            new("Cloudflare DNS", "1.1.1.1"),
            new("localhost", "127.0.0.1")
        };

        var results = new List<object>();
        var failedServices = new List<string>();

        foreach (var service in externalServices)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(service.Host, 5000);
                
                if (reply.Status == IPStatus.Success)
                {
                    results.Add(new
                    {
                        name = service.Name,
                        host = service.Host,
                        status = "healthy",
                        roundtrip_ms = reply.RoundtripTime
                    });
                }
                else
                {
                    failedServices.Add($"{service.Name} ({reply.Status})");
                    results.Add(new
                    {
                        name = service.Name,
                        host = service.Host,
                        status = "unhealthy",
                        error = reply.Status.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                failedServices.Add($"{service.Name} ({ex.Message})");
                results.Add(new
                {
                    name = service.Name,
                    host = service.Host,
                    status = "error",
                    error = ex.Message
                });
            }
        }

        var data = new Dictionary<string, object>
        {
            ["external_services"] = results
        };

        if (failedServices.Count == externalServices.Count)
        {
            return HealthCheckResult.Unhealthy(
                "All external services unreachable", data: data);
        }

        if (failedServices.Any())
        {
            return HealthCheckResult.Degraded(
                $"Some external services unreachable: {string.Join(", ", failedServices)}", data: data);
        }

        return HealthCheckResult.Healthy(
            "All external services reachable", data: data);
    }

    public async Task<ComprehensiveHealthReport> GetComprehensiveHealthReportAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        var healthChecks = await Task.WhenAll(
            CheckDatabaseHealthAsync(),
            CheckMemoryHealthAsync(),
            CheckDiskSpaceHealthAsync(),
            CheckExternalServicesHealthAsync()
        );

        stopwatch.Stop();

        var overallStatus = healthChecks.All(h => h.Status == HealthStatus.Healthy) 
            ? HealthStatus.Healthy
            : healthChecks.Any(h => h.Status == HealthStatus.Unhealthy)
                ? HealthStatus.Unhealthy
                : HealthStatus.Degraded;

        return new ComprehensiveHealthReport
        {
            OverallStatus = overallStatus.ToString(),
            CheckDuration = stopwatch.Elapsed,
            Timestamp = DateTime.UtcNow,
            DatabaseHealth = healthChecks[0],
            MemoryHealth = healthChecks[1],
            DiskSpaceHealth = healthChecks[2],
            ExternalServicesHealth = healthChecks[3],
            SystemInfo = await GetSystemInfoAsync()
        };
    }

    public async Task<SystemMetrics> GetSystemMetricsAsync()
    {
        await Task.Delay(1); // Make async
        
        var process = Process.GetCurrentProcess();
        
        var metrics = new SystemMetrics
        {
            Timestamp = DateTime.UtcNow,
            ProcessId = process.Id,
            WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
            PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            UptimeSeconds = (DateTime.UtcNow - process.StartTime).TotalSeconds,
            GCMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };

        // Add CPU usage if available
        if (_cpuCounter != null)
        {
            try
            {
                metrics.CpuUsagePercent = _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get CPU usage");
            }
        }

        // Add available memory if available
        if (_memoryCounter != null)
        {
            try
            {
                metrics.AvailableMemoryMB = _memoryCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get available memory");
            }
        }

        return metrics;
    }

    private async Task<SystemInfo> GetSystemInfoAsync()
    {
        await Task.Delay(1); // Make async
        
        var assembly = Assembly.GetExecutingAssembly();
        var process = Process.GetCurrentProcess();
        
        return new SystemInfo
        {
            ApplicationVersion = assembly.GetName().Version?.ToString() ?? "Unknown",
            RuntimeVersion = Environment.Version.ToString(),
            OperatingSystem = Environment.OSVersion.ToString(),
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            StartTime = process.StartTime,
            Uptime = DateTime.UtcNow - process.StartTime
        };
    }
}

// Supporting classes
public record ExternalServiceCheck(string Name, string Host);

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

public class SystemMetrics
{
    public DateTime Timestamp { get; set; }
    public int ProcessId { get; set; }
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public double UptimeSeconds { get; set; }
    public long GCMemoryMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public float? CpuUsagePercent { get; set; }
    public float? AvailableMemoryMB { get; set; }
}

public class SystemInfo
{
    public string ApplicationVersion { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime { get; set; }
}
