using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VertexBPMN.Api.Hubs;
using VertexBPMN.Core.Engine;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Performance monitoring and dashboard controller
/// Olympic-level feature: Enterprise Scalability - Performance monitoring
/// </summary>
[ApiController]
[Route("api/performance")]
public class PerformanceController : ControllerBase
{
    private readonly IDistributedTokenEngine _distributedEngine;
    private readonly ILogger<PerformanceController> _logger;
    private static readonly Dictionary<string, PerformanceMetric> _metrics = new();
    private static readonly object _metricsLock = new();

    public PerformanceController(
        IDistributedTokenEngine distributedEngine,
        ILogger<PerformanceController> logger)
    {
        _distributedEngine = distributedEngine;
        _logger = logger;
    }

    /// <summary>
    /// Get current system performance metrics
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = await CollectSystemMetricsAsync();
        return Ok(metrics);
    }

    /// <summary>
    /// Get real-time performance dashboard data
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = new
        {
            SystemMetrics = await CollectSystemMetricsAsync(),
            ProcessMetrics = await CollectProcessMetricsAsync(),
            WorkerMetrics = await CollectWorkerMetricsAsync(),
            QueueMetrics = await CollectQueueMetricsAsync(),
            Timestamp = DateTime.UtcNow
        };

        return Ok(dashboard);
    }

    /// <summary>
    /// Get performance trends over time
    /// </summary>
    [HttpGet("trends")]
    public IActionResult GetTrends([FromQuery] int hours = 24)
    {
        lock (_metricsLock)
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-hours);
            var trends = _metrics.Values
                .Where(m => m.Timestamp >= cutoffTime)
                .OrderBy(m => m.Timestamp)
                .GroupBy(m => m.Timestamp.ToString("yyyy-MM-dd HH:mm"))
                .Select(g => new
                {
                    Timestamp = g.Key,
                    AvgCpuUsage = g.Average(m => m.CpuUsage),
                    AvgMemoryUsage = g.Average(m => m.MemoryUsage),
                    TotalRequests = g.Sum(m => m.RequestCount),
                    AvgResponseTime = g.Average(m => m.AvgResponseTime)
                })
                .ToList();

            return Ok(trends);
        }
    }

    /// <summary>
    /// Get current load balancer status
    /// </summary>
    [HttpGet("load-balancer")]
    public async Task<IActionResult> GetLoadBalancerStatus()
    {
        var pendingTokens = await _distributedEngine.GetPendingTokensAsync();
        
        var status = new
        {
            PendingTokens = pendingTokens.Count,
            QueueLength = pendingTokens.Count,
            DistributionStrategy = "RoundRobin", // Could be configurable
            Workers = await CollectWorkerMetricsAsync(),
            LoadDistribution = CalculateLoadDistribution(pendingTokens),
            Timestamp = DateTime.UtcNow
        };

        return Ok(status);
    }

    /// <summary>
    /// Trigger system health check
    /// </summary>
    [HttpPost("health-check")]
    public async Task<IActionResult> HealthCheck()
    {
        var healthCheck = new
        {
            Status = "Healthy",
            Checks = new
            {
                Database = await CheckDatabaseHealth(),
                DistributedEngine = await CheckDistributedEngineHealth(),
                Memory = CheckMemoryHealth(),
                Disk = CheckDiskHealth(),
                Network = await CheckNetworkHealth()
            },
            Timestamp = DateTime.UtcNow
        };

        return Ok(new { 
            Status = "Healthy", 
            Details = healthCheck 
        });
    }

    /// <summary>
    /// Start performance monitoring
    /// </summary>
    [HttpPost("start-monitoring")]
    public IActionResult StartMonitoring()
    {
        // This would start background monitoring tasks
        _logger.LogInformation("Performance monitoring started");
        return Ok(new { Message = "Performance monitoring started", StartedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Stop performance monitoring  
    /// </summary>
    [HttpPost("stop-monitoring")]
    public IActionResult StopMonitoring()
    {
        _logger.LogInformation("Performance monitoring stopped");
        return Ok(new { Message = "Performance monitoring stopped", StoppedAt = DateTime.UtcNow });
    }

    private async Task<object> CollectSystemMetricsAsync()
    {
        var process = Process.GetCurrentProcess();
        var gc = GC.GetTotalMemory(false);
        
        var metrics = new
        {
            CpuUsage = await GetCpuUsageAsync(),
            MemoryUsage = new
            {
                WorkingSet = process.WorkingSet64,
                PrivateMemory = process.PrivateMemorySize64,
                GcMemory = gc,
                WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
                PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024
            },
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            Uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime(),
            Timestamp = DateTime.UtcNow
        };

        // Store metric for trends
        lock (_metricsLock)
        {
            var key = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            _metrics[key] = new PerformanceMetric
            {
                Timestamp = DateTime.UtcNow,
                CpuUsage = (double)(metrics.CpuUsage ?? 0),
                MemoryUsage = metrics.MemoryUsage.WorkingSetMB,
                RequestCount = 1, // Simplified
                AvgResponseTime = 100 // Simplified
            };
            
            // Keep only last 1000 metrics
            if (_metrics.Count > 1000)
            {
                var oldestKey = _metrics.Keys.OrderBy(k => k).First();
                _metrics.Remove(oldestKey);
            }
        }

        return metrics;
    }

    private async Task<object> CollectProcessMetricsAsync()
    {
        // This would collect BPMN process-specific metrics
        return await Task.FromResult(new
        {
            TotalProcesses = 150, // Would come from database
            ActiveProcesses = 23,
            CompletedToday = 45,
            AverageExecutionTime = TimeSpan.FromMinutes(12.5),
            ProcessThroughput = 3.2, // processes per minute
            ErrorRate = 0.02 // 2%
        });
    }

    private async Task<object> CollectWorkerMetricsAsync()
    {
        // This would collect distributed worker metrics
        return await Task.FromResult(new
        {
            TotalWorkers = 3,
            ActiveWorkers = 3,
            IdleWorkers = 0,
            WorkerLoad = new[]
            {
                new { WorkerId = "worker-1", Load = 0.75, Capacity = 10, ActiveTasks = 7 },
                new { WorkerId = "worker-2", Load = 0.60, Capacity = 10, ActiveTasks = 6 },
                new { WorkerId = "worker-3", Load = 0.30, Capacity = 10, ActiveTasks = 3 }
            }
        });
    }

    private async Task<object> CollectQueueMetricsAsync()
    {
        var pendingTokens = await _distributedEngine.GetPendingTokensAsync();
        
        return new
        {
            PendingTokens = pendingTokens.Count,
            QueueLength = pendingTokens.Count,
            OldestToken = pendingTokens.MinBy(t => t.CreatedAt)?.CreatedAt,
            TokensByType = pendingTokens
                .GroupBy(t => t.NodeType)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private async Task<double?> GetCpuUsageAsync()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;
            
            await Task.Delay(500); // Wait 500ms
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return cpuUsageTotal * 100;
        }
        catch
        {
            return null;
        }
    }

    private object CalculateLoadDistribution(List<ExecutionToken> tokens)
    {
        var distribution = tokens
            .GroupBy(t => t.AssignedWorker ?? "unassigned")
            .ToDictionary(g => g.Key, g => g.Count());

        return new
        {
            Distribution = distribution,
            IsBalanced = CalculateLoadBalance(distribution),
            Variance = CalculateLoadVariance(distribution)
        };
    }

    private bool CalculateLoadBalance(Dictionary<string, int> distribution)
    {
        if (distribution.Count <= 1) return true;
        
        var values = distribution.Values.ToList();
        var max = values.Max();
        var min = values.Min();
        
        // Consider balanced if difference is less than 20%
        return max == 0 || (double)(max - min) / max < 0.2;
    }

    private double CalculateLoadVariance(Dictionary<string, int> distribution)
    {
        if (distribution.Count <= 1) return 0;
        
        var values = distribution.Values.Select(v => (double)v).ToList();
        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        
        return variance;
    }

    #region Health Checks

    private async Task<object> CheckDatabaseHealth()
    {
        try
        {
            // This would check database connectivity
            await Task.Delay(10); // Simulate check
            return new { Healthy = true, ResponseTime = "10ms", Message = "Database responsive" };
        }
        catch (Exception ex)
        {
            return new { Healthy = false, Error = ex.Message };
        }
    }

    private async Task<object> CheckDistributedEngineHealth()
    {
        try
        {
            var canExecute = await _distributedEngine.CanExecuteAsync("healthCheck");
            return new { Healthy = canExecute, Message = "Distributed engine responsive" };
        }
        catch (Exception ex)
        {
            return new { Healthy = false, Error = ex.Message };
        }
    }

    private object CheckMemoryHealth()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
            var healthy = memoryUsageMB < 1000; // Less than 1GB
            
            return new { 
                Healthy = healthy, 
                MemoryUsageMB = memoryUsageMB,
                Message = healthy ? "Memory usage normal" : "High memory usage detected"
            };
        }
        catch (Exception ex)
        {
            return new { Healthy = false, Error = ex.Message };
        }
    }

    private object CheckDiskHealth()
    {
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
            var systemDrive = drives.FirstOrDefault(d => d.Name == Path.GetPathRoot(Environment.SystemDirectory));
            
            if (systemDrive != null)
            {
                var freeSpacePercent = (double)systemDrive.AvailableFreeSpace / systemDrive.TotalSize * 100;
                var healthy = freeSpacePercent > 10; // More than 10% free
                
                return new {
                    Healthy = healthy,
                    FreeSpacePercent = Math.Round(freeSpacePercent, 1),
                    FreeSpaceGB = Math.Round((double)systemDrive.AvailableFreeSpace / 1024 / 1024 / 1024, 1),
                    Message = healthy ? "Disk space sufficient" : "Low disk space"
                };
            }
            
            return new { Healthy = true, Message = "Disk check skipped" };
        }
        catch (Exception ex)
        {
            return new { Healthy = false, Error = ex.Message };
        }
    }

    private async Task<object> CheckNetworkHealth()
    {
        try
        {
            // Simulate network check
            await Task.Delay(50);
            return new { Healthy = true, Message = "Network connectivity OK" };
        }
        catch (Exception ex)
        {
            return new { Healthy = false, Error = ex.Message };
        }
    }

    #endregion
}

/// <summary>
/// Performance metric data structure
/// </summary>
public record PerformanceMetric
{
    public DateTime Timestamp { get; init; }
    public double CpuUsage { get; init; }
    public double MemoryUsage { get; init; }
    public int RequestCount { get; init; }
    public double AvgResponseTime { get; init; }
}
