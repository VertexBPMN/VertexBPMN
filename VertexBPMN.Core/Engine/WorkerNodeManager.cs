using Microsoft.Extensions.Logging;

namespace VertexBPMN.Core.Engine;

/// <summary>
/// Worker node management interface for distributed execution
/// Olympic-level feature: Enterprise Scalability - Worker Management
/// </summary>
public interface IWorkerNodeManager
{
    Task<WorkerNode> RegisterWorkerAsync(WorkerRegistrationRequest request);
    Task UnregisterWorkerAsync(string workerId);
    Task<List<WorkerNode>> GetActiveWorkersAsync();
    Task<WorkerNode?> GetWorkerAsync(string workerId);
    Task UpdateWorkerStatusAsync(string workerId, WorkerStatus status);
    Task<WorkerCapacityInfo> GetWorkerCapacityAsync(string workerId);
    Task<bool> IsWorkerHealthyAsync(string workerId);
    Task<List<WorkerNode>> GetWorkersForNodeTypeAsync(string nodeType);
    Task NotifyWorkersAsync(string message);
    Task<WorkerPerformanceMetrics> GetWorkerPerformanceAsync(string workerId);
}

/// <summary>
/// Worker registration request
/// </summary>
public record WorkerRegistrationRequest(
    string WorkerId,
    string HostName,
    int Port,
    List<string> SupportedNodeTypes,
    int MaxCapacity
);

/// <summary>
/// Worker status information
/// </summary>
public record WorkerStatus(
    int CurrentLoad,
    double CpuUsage,
    double MemoryUsage,
    int ActiveTasks,
    DateTime Timestamp
);

/// <summary>
/// Worker capacity information
/// </summary>
public record WorkerCapacityInfo(
    string WorkerId,
    bool IsAvailable,
    int CurrentLoad,
    int MaxCapacity,
    int AvailableCapacity,
    double UtilizationPercentage = 0
);

/// <summary>
/// Worker performance metrics
/// </summary>
public record WorkerPerformanceMetrics
{
    public string WorkerId { get; init; } = string.Empty;
    public DateTime RegisteredAt { get; init; }
    public int TotalTasksProcessed { get; init; }
    public TimeSpan AverageProcessingTime { get; init; }
    public DateTime LastActivity { get; init; }
    public double CpuUsage { get; init; }
    public double MemoryUsage { get; init; }
    public int ActiveTasks { get; init; }
    public TimeSpan Uptime => DateTime.UtcNow - RegisteredAt;
    public double TasksPerMinute => TotalTasksProcessed > 0 && Uptime.TotalMinutes > 0 
        ? TotalTasksProcessed / Uptime.TotalMinutes 
        : 0;
}

/// <summary>
/// Worker node manager implementation
/// </summary>
public class WorkerNodeManager : IWorkerNodeManager
{
    private readonly Dictionary<string, WorkerNode> _workers = new();
    private readonly Dictionary<string, WorkerPerformanceMetrics> _performance = new();
    private readonly object _lock = new();
    private readonly ILogger<WorkerNodeManager> _logger;
    private readonly Timer _healthCheckTimer;

    public WorkerNodeManager(ILogger<WorkerNodeManager> logger)
    {
        _logger = logger;
        
        // Start health check timer
        _healthCheckTimer = new Timer(CheckWorkerHealth, null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public Task<WorkerNode> RegisterWorkerAsync(WorkerRegistrationRequest request)
    {
        var worker = new WorkerNode(
            request.WorkerId,
            request.HostName,
            request.Port,
            DateTime.UtcNow,
            request.SupportedNodeTypes,
            0,
            request.MaxCapacity
        );

        lock (_lock)
        {
            _workers[worker.Id] = worker;
            _performance[worker.Id] = new WorkerPerformanceMetrics
            {
                WorkerId = worker.Id,
                RegisteredAt = DateTime.UtcNow,
                TotalTasksProcessed = 0,
                AverageProcessingTime = TimeSpan.Zero,
                LastActivity = DateTime.UtcNow
            };
        }

        _logger.LogInformation("Worker {WorkerId} registered from {HostName}:{Port} with capacity {Capacity}",
            worker.Id, worker.HostName, worker.Port, worker.MaxCapacity);

        return Task.FromResult(worker);
    }

    public Task UnregisterWorkerAsync(string workerId)
    {
        WorkerNode? removedWorker = null;
        
        lock (_lock)
        {
            if (_workers.TryGetValue(workerId, out removedWorker))
            {
                _workers.Remove(workerId);
                _performance.Remove(workerId);
            }
        }

        if (removedWorker != null)
        {
            _logger.LogInformation("Worker {WorkerId} unregistered", workerId);
        }
        
        return Task.CompletedTask;
    }

    public Task<List<WorkerNode>> GetActiveWorkersAsync()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-2); // 2 minutes timeout
            var activeWorkers = _workers.Values
                .Where(w => w.LastHeartbeat > cutoff)
                .ToList();
            return Task.FromResult(activeWorkers);
        }
    }

    public Task<WorkerNode?> GetWorkerAsync(string workerId)
    {
        lock (_lock)
        {
            var worker = _workers.TryGetValue(workerId, out var w) ? w : null;
            return Task.FromResult(worker);
        }
    }

    public Task UpdateWorkerStatusAsync(string workerId, WorkerStatus status)
    {
        lock (_lock)
        {
            if (_workers.TryGetValue(workerId, out var worker))
            {
                _workers[workerId] = worker with 
                { 
                    LastHeartbeat = DateTime.UtcNow,
                    CurrentLoad = status.CurrentLoad
                };

                // Update performance metrics
                if (_performance.TryGetValue(workerId, out var metrics))
                {
                    _performance[workerId] = metrics with
                    {
                        LastActivity = DateTime.UtcNow,
                        CpuUsage = status.CpuUsage,
                        MemoryUsage = status.MemoryUsage,
                        ActiveTasks = status.ActiveTasks
                    };
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<WorkerCapacityInfo> GetWorkerCapacityAsync(string workerId)
    {
        lock (_lock)
        {
            if (!_workers.TryGetValue(workerId, out var worker))
            {
                var unavailable = new WorkerCapacityInfo(workerId, false, 0, 0, 0);
                return Task.FromResult(unavailable);
            }

            var availableCapacity = worker.MaxCapacity - worker.CurrentLoad;
            var utilizationPercentage = worker.MaxCapacity > 0 
                ? (double)worker.CurrentLoad / worker.MaxCapacity * 100 
                : 0;

            var capacity = new WorkerCapacityInfo(
                workerId,
                true,
                worker.CurrentLoad,
                worker.MaxCapacity,
                availableCapacity,
                Math.Round(utilizationPercentage, 1)
            );
            return Task.FromResult(capacity);
        }
    }

    public Task<bool> IsWorkerHealthyAsync(string workerId)
    {
        lock (_lock)
        {
            if (!_workers.TryGetValue(workerId, out var worker))
                return Task.FromResult(false);

            var timeSinceHeartbeat = DateTime.UtcNow - worker.LastHeartbeat;
            var isHealthy = timeSinceHeartbeat < TimeSpan.FromMinutes(2);
            return Task.FromResult(isHealthy);
        }
    }

    public async Task<List<WorkerNode>> GetWorkersForNodeTypeAsync(string nodeType)
    {
        var activeWorkers = await GetActiveWorkersAsync();
        return activeWorkers
            .Where(w => w.SupportedNodeTypes.Contains(nodeType))
            .OrderBy(w => w.CurrentLoad)
            .ToList();
    }

    public Task NotifyWorkersAsync(string message)
    {
        List<WorkerNode> workers;
        lock (_lock)
        {
            workers = _workers.Values.ToList();
        }

        _logger.LogInformation("Broadcasting message to {WorkerCount} workers: {Message}", 
            workers.Count, message);

        // In a real implementation, this would send HTTP notifications to workers
        foreach (var worker in workers)
        {
            _logger.LogDebug("Notified worker {WorkerId} at {HostName}:{Port}", 
                worker.Id, worker.HostName, worker.Port);
        }

        return Task.CompletedTask;
    }

    public Task<WorkerPerformanceMetrics> GetWorkerPerformanceAsync(string workerId)
    {
        lock (_lock)
        {
            if (_performance.TryGetValue(workerId, out var metrics))
            {
                return Task.FromResult(metrics);
            }

            var defaultMetrics = new WorkerPerformanceMetrics
            {
                WorkerId = workerId,
                RegisteredAt = DateTime.UtcNow,
                TotalTasksProcessed = 0,
                AverageProcessingTime = TimeSpan.Zero,
                LastActivity = DateTime.MinValue
            };
            return Task.FromResult(defaultMetrics);
        }
    }

    public Task RecordTaskCompletionAsync(string workerId, TimeSpan processingTime)
    {
        lock (_lock)
        {
            if (_performance.TryGetValue(workerId, out var metrics))
            {
                var totalTasks = metrics.TotalTasksProcessed + 1;
                var totalTime = metrics.AverageProcessingTime.TotalMilliseconds * metrics.TotalTasksProcessed + 
                               processingTime.TotalMilliseconds;
                var newAverage = TimeSpan.FromMilliseconds(totalTime / totalTasks);

                _performance[workerId] = metrics with
                {
                    TotalTasksProcessed = totalTasks,
                    AverageProcessingTime = newAverage,
                    LastActivity = DateTime.UtcNow
                };
            }
        }
        
        return Task.CompletedTask;
    }

    private async void CheckWorkerHealth(object? state)
    {
        try
        {
            List<string> unhealthyWorkers = new();
            
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-2);
                unhealthyWorkers = _workers.Values
                    .Where(w => w.LastHeartbeat < cutoff)
                    .Select(w => w.Id)
                    .ToList();
            }

            foreach (var workerId in unhealthyWorkers)
            {
                _logger.LogWarning("Worker {WorkerId} appears unhealthy, removing from active pool", workerId);
                await UnregisterWorkerAsync(workerId);
            }

            var activeCount = (await GetActiveWorkersAsync()).Count;
            _logger.LogDebug("Worker health check completed: {ActiveWorkers} active, {RemovedWorkers} removed", 
                activeCount, unhealthyWorkers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during worker health check");
        }
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
    }
}
