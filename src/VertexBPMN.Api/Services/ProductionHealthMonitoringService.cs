using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.Services;

internal sealed class ProductionHealthMonitoringService : IHealthMonitoringService, IDisposable
{
    private readonly ILogger<ProductionHealthMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;

    public void Dispose()
    {
        try
        {
            _cpuCounter?.Dispose();
        }
        finally
        {
            _memoryCounter?.Dispose();
        }
    }

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
            
            Type[] databaseTypes = [typeof(BpmnDbContext), typeof(TenantDbContext),
                typeof(SimulationScenarioDbContext), typeof(ProcessMiningEventDbContext), typeof(DecisionDbContext)];
            // Separate contexts can be checked concurrently. Bound the entire database check
            // instead of accumulating five provider timeouts in the Studio HTTP pipeline.
            var checks = await Task.WhenAll(databaseTypes.Select(async databaseType =>
            {
                var dbName = databaseType.Name;
                try
                {
                    if (scope.ServiceProvider.GetService(databaseType) is not DbContext dbContext)
                    {
                        return (Healthy: false, Message: $"{dbName}: Not registered");
                    }
                    var stopwatch = Stopwatch.StartNew();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var connected = await dbContext.Database.CanConnectAsync(timeout.Token);
                    stopwatch.Stop();
                    return (Healthy: connected, Message: connected
                        ? $"{dbName} ({stopwatch.ElapsedMilliseconds}ms)" : $"{dbName}: Cannot connect");
                }
                catch (Exception)
                {
                    // Provider messages can contain hosts, usernames or connection data.
                    return (Healthy: false, Message: $"{dbName}: Connection check failed or timed out");
                }
            }));
            var healthyDatabases = checks.Where(check => check.Healthy).Select(check => check.Message).ToList();
            var unhealthyDatabases = checks.Where(check => !check.Healthy).Select(check => check.Message).ToList();

            if (unhealthyDatabases.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"Database issues: {string.Join(", ", unhealthyDatabases)}",
                    data: new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["healthy_databases"] = healthyDatabases,
                        ["unhealthy_databases"] = unhealthyDatabases
                    });
            }

            return HealthCheckResult.Healthy(
                $"All databases healthy: {string.Join(", ", healthyDatabases)}",
                data: new Dictionary<string, object>(StringComparer.Ordinal)
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
            
            var data = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["working_set_mb"] = workingSetMB,
                ["private_memory_mb"] = privateMemoryMB,
                ["gc_memory_mb"] = gc,
                ["gen0_collections"] = GC.CollectionCount(0),
                ["gen1_collections"] = GC.CollectionCount(1),
                ["gen2_collections"] = GC.CollectionCount(2)
            };

            // Check thresholds
            if (workingSetMB > 2000) // Check the critical threshold before the warning.
            {
                return HealthCheckResult.Unhealthy(
                    $"Critical memory usage: {workingSetMB}MB working set", data: data);
            }

            if (workingSetMB > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"High memory usage: {workingSetMB}MB working set", data: data);
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
                var usedPercentage = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100;
                driveInfos.Add(GetDriveMetrics(drive, usedPercentage));

                if (usedPercentage > 95)
                {
                    criticalDrives.Add($"{drive.Name} ({usedPercentage:F1}% used)");
                }
                else if (usedPercentage > 85)
                {
                    lowSpaceDrives.Add($"{drive.Name} ({usedPercentage:F1}% used)");
                }
            }

            var data = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["drives"] = driveInfos
            };

            if (criticalDrives.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"Critical disk space: {string.Join(", ", criticalDrives)}", data: data);
            }

            if (lowSpaceDrives.Count > 0)
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
        var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        var externalServices = configuration.GetSection("HealthMonitoring:ExternalServices").Get<ExternalServiceCheck[]>() ?? [];
        if (externalServices.Length == 0)
        {
            return HealthCheckResult.Healthy("No optional external ICMP probes configured");
        }

        if (externalServices.Length > 16 || externalServices.Any(service => service is null || string.IsNullOrWhiteSpace(service.Name) || string.IsNullOrWhiteSpace(service.Host)))
        {
            return HealthCheckResult.Unhealthy("External ICMP probe configuration is invalid (maximum 16 named hosts).");
        }

        var checks = await Task.WhenAll(externalServices.Select(CheckExternalServiceAsync));
        var failedServices = checks.Where(check => !check.Healthy).Select(check => check.Name).ToList();
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["external_services"] = checks.Select(check => check.Result).ToList()
        };

        if (failedServices.Count == externalServices.Length)
        {
            return HealthCheckResult.Unhealthy(
                "All external services unreachable", data: data);
        }

        if (failedServices.Count > 0)
        {
            return HealthCheckResult.Degraded(
                $"Some external services unreachable: {string.Join(", ", failedServices)}", data: data);
        }

        return HealthCheckResult.Healthy(
            "All external services reachable", data: data);
    }

    private static object GetDriveMetrics(DriveInfo drive, double usedPercentage) => new
    {
        name = drive.Name,
        total_gb = drive.TotalSize / 1024 / 1024 / 1024,
        free_gb = drive.AvailableFreeSpace / 1024 / 1024 / 1024,
        used_percentage = Math.Round(usedPercentage, 1)
    };

    private static async Task<(bool Healthy, string Name, object Result)> CheckExternalServiceAsync(ExternalServiceCheck service)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(service.Host, TimeSpan.FromSeconds(1)).WaitAsync(TimeSpan.FromSeconds(2));
            var healthy = reply.Status == IPStatus.Success;
            object result = healthy
                ? new { name = service.Name, host = service.Host, status = "healthy", roundtrip_ms = reply.RoundtripTime }
                : new { name = service.Name, host = service.Host, status = "unhealthy", error = reply.Status.ToString() };
            return (healthy, service.Name, result);
        }
        catch (Exception)
        {
            return (false, service.Name, new { name = service.Name, host = service.Host, status = "error", error = "ICMP probe failed or timed out" });
        }
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

        var overallStatus = HealthStatus.Degraded;
        if (healthChecks.All(h => h.Status == HealthStatus.Healthy))
        {
            overallStatus = HealthStatus.Healthy;
        }
        else if (healthChecks.Any(h => h.Status == HealthStatus.Unhealthy))
        {
            overallStatus = HealthStatus.Unhealthy;
        }

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
            UptimeSeconds = GetUptime(process).TotalSeconds,
            GCMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };

        // Add CPU usage if available
        if (OperatingSystem.IsWindows() && _cpuCounter != null)
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
        if (OperatingSystem.IsWindows() && _memoryCounter != null)
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
            StartTime = process.StartTime.ToUniversalTime(),
            Uptime = GetUptime(process)
        };
    }

    private static TimeSpan GetUptime(Process process) =>
        TimeSpan.FromSeconds(Math.Max(0, (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds));
}
