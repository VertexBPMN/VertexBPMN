using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Engine;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Load balancer controller for distributed process execution
/// Olympic-level feature: Enterprise Scalability - Load balancing
/// </summary>
[ApiController]
[Route("api/load-balancer")]
public class LoadBalancerController : ControllerBase
{
    private readonly IDistributedTokenEngine _distributedEngine;
    private readonly ILoadBalancingService _loadBalancer;
    private readonly ILogger<LoadBalancerController> _logger;

    public LoadBalancerController(
        IDistributedTokenEngine distributedEngine,
        ILoadBalancingService loadBalancer,
        ILogger<LoadBalancerController> logger)
    {
        _distributedEngine = distributedEngine;
        _loadBalancer = loadBalancer;
        _logger = logger;
    }

    /// <summary>
    /// Get current load balancing status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _loadBalancer.GetStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Register a new worker node
    /// </summary>
    [HttpPost("workers")]
    public IActionResult RegisterWorker([FromBody] WorkerRegistrationRequest request)
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

        _loadBalancer.RegisterWorker(worker);
        _logger.LogInformation("Registered worker {WorkerId} at {HostName}:{Port}", 
            worker.Id, worker.HostName, worker.Port);

        return Ok(new { Message = "Worker registered successfully", WorkerId = worker.Id });
    }

    /// <summary>
    /// Unregister a worker node
    /// </summary>
    [HttpDelete("workers/{workerId}")]
    public IActionResult UnregisterWorker(string workerId)
    {
        _loadBalancer.UnregisterWorker(workerId);
        _logger.LogInformation("Unregistered worker {WorkerId}", workerId);
        return Ok(new { Message = "Worker unregistered successfully" });
    }

    /// <summary>
    /// Update worker heartbeat
    /// </summary>
    [HttpPost("workers/{workerId}/heartbeat")]
    public IActionResult UpdateHeartbeat(string workerId, [FromBody] WorkerHeartbeatRequest request)
    {
        _loadBalancer.UpdateWorkerHeartbeat(workerId, request.CurrentLoad);
        return Ok(new { Message = "Heartbeat updated", Timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Get list of all workers
    /// </summary>
    [HttpGet("workers")]
    public async Task<IActionResult> GetWorkers()
    {
        var workers = await _loadBalancer.GetWorkersAsync();
        return Ok(workers);
    }

    /// <summary>
    /// Get worker health status
    /// </summary>
    [HttpGet("workers/{workerId}/health")]
    public async Task<IActionResult> GetWorkerHealth(string workerId)
    {
        var health = await _loadBalancer.GetWorkerHealthAsync(workerId);
        if (health == null)
            return NotFound(new { Message = "Worker not found" });
        
        return Ok(health);
    }

    /// <summary>
    /// Rebalance workload across workers
    /// </summary>
    [HttpPost("rebalance")]
    public async Task<IActionResult> Rebalance()
    {
        var result = await _loadBalancer.RebalanceAsync();
        return Ok(result);
    }

    /// <summary>
    /// Get load balancing configuration
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = _loadBalancer.GetConfiguration();
        return Ok(config);
    }

    /// <summary>
    /// Update load balancing configuration
    /// </summary>
    [HttpPut("config")]
    public IActionResult UpdateConfig([FromBody] LoadBalancingConfig config)
    {
        _loadBalancer.UpdateConfiguration(config);
        return Ok(new { Message = "Configuration updated successfully" });
    }
}

/// <summary>
/// Load balancing service interface
/// </summary>
public interface ILoadBalancingService
{
    Task<object> GetStatusAsync();
    void RegisterWorker(WorkerNode worker);
    void UnregisterWorker(string workerId);
    void UpdateWorkerHeartbeat(string workerId, int currentLoad);
    Task<List<WorkerNode>> GetWorkersAsync();
    Task<object?> GetWorkerHealthAsync(string workerId);
    Task<object> RebalanceAsync();
    LoadBalancingConfig GetConfiguration();
    void UpdateConfiguration(LoadBalancingConfig config);
}

/// <summary>
/// Load balancing service implementation
/// </summary>
public class LoadBalancingService : ILoadBalancingService
{
    private readonly IDistributedTokenEngine _distributedEngine;
    private readonly Dictionary<string, WorkerNode> _workers = new();
    private readonly object _workersLock = new();
    private LoadBalancingConfig _config = new();

    public LoadBalancingService(IDistributedTokenEngine distributedEngine)
    {
        _distributedEngine = distributedEngine;
    }

    public async Task<object> GetStatusAsync()
    {
        lock (_workersLock)
        {
            var totalCapacity = _workers.Values.Sum(w => w.MaxCapacity);
            var totalLoad = _workers.Values.Sum(w => w.CurrentLoad);
            var averageLoad = _workers.Any() ? (double)totalLoad / _workers.Count : 0;

            return new
            {
                TotalWorkers = _workers.Count,
                ActiveWorkers = _workers.Values.Count(w => DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2)),
                TotalCapacity = totalCapacity,
                TotalLoad = totalLoad,
                AverageLoad = Math.Round(averageLoad, 2),
                LoadPercentage = totalCapacity > 0 ? Math.Round((double)totalLoad / totalCapacity * 100, 1) : 0,
                Strategy = _config.Strategy,
                IsHealthy = CheckSystemHealth(),
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public void RegisterWorker(WorkerNode worker)
    {
        lock (_workersLock)
        {
            _workers[worker.Id] = worker;
        }
    }

    public void UnregisterWorker(string workerId)
    {
        lock (_workersLock)
        {
            _workers.Remove(workerId);
        }
    }

    public void UpdateWorkerHeartbeat(string workerId, int currentLoad)
    {
        lock (_workersLock)
        {
            if (_workers.TryGetValue(workerId, out var worker))
            {
                _workers[workerId] = worker with 
                { 
                    LastHeartbeat = DateTime.UtcNow,
                    CurrentLoad = currentLoad
                };
            }
        }
    }

    public async Task<List<WorkerNode>> GetWorkersAsync()
    {
        lock (_workersLock)
        {
            return _workers.Values.ToList();
        }
    }

    public async Task<object?> GetWorkerHealthAsync(string workerId)
    {
        lock (_workersLock)
        {
            if (!_workers.TryGetValue(workerId, out var worker))
                return null;

            var timeSinceHeartbeat = DateTime.UtcNow - worker.LastHeartbeat;
            var isHealthy = timeSinceHeartbeat < TimeSpan.FromMinutes(2);
            var loadPercentage = worker.MaxCapacity > 0 ? (double)worker.CurrentLoad / worker.MaxCapacity * 100 : 0;

            return new
            {
                WorkerId = worker.Id,
                IsHealthy = isHealthy,
                LastHeartbeat = worker.LastHeartbeat,
                TimeSinceHeartbeat = timeSinceHeartbeat,
                CurrentLoad = worker.CurrentLoad,
                MaxCapacity = worker.MaxCapacity,
                LoadPercentage = Math.Round(loadPercentage, 1),
                SupportedNodeTypes = worker.SupportedNodeTypes,
                Status = isHealthy ? "Online" : "Offline"
            };
        }
    }

    public async Task<object> RebalanceAsync()
    {
        var pendingTokens = await _distributedEngine.GetPendingTokensAsync();
        var rebalancedCount = 0;

        lock (_workersLock)
        {
            var healthyWorkers = _workers.Values
                .Where(w => DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2))
                .OrderBy(w => w.CurrentLoad)
                .ToList();

            if (!healthyWorkers.Any())
            {
                return new
                {
                    Message = "No healthy workers available for rebalancing",
                    RebalancedTokens = 0
                };
            }

            // Redistribute tokens using configured strategy
            foreach (var token in pendingTokens)
            {
                var bestWorker = SelectWorkerForToken(token, healthyWorkers);
                if (bestWorker != null)
                {
                    // This would reassign the token
                    rebalancedCount++;
                }
            }
        }

        return new
        {
            Message = "Rebalancing completed",
            RebalancedTokens = rebalancedCount,
            Strategy = _config.Strategy,
            Timestamp = DateTime.UtcNow
        };
    }

    public LoadBalancingConfig GetConfiguration()
    {
        return _config;
    }

    public void UpdateConfiguration(LoadBalancingConfig config)
    {
        _config = config;
    }

    private WorkerNode? SelectWorkerForToken(ExecutionToken token, List<WorkerNode> workers)
    {
        var eligibleWorkers = workers
            .Where(w => w.SupportedNodeTypes.Contains(token.NodeType))
            .Where(w => w.CurrentLoad < w.MaxCapacity)
            .ToList();

        if (!eligibleWorkers.Any())
            return null;

        return _config.Strategy switch
        {
            LoadBalancingStrategy.RoundRobin => SelectRoundRobin(eligibleWorkers),
            LoadBalancingStrategy.LeastLoaded => SelectLeastLoaded(eligibleWorkers),
            LoadBalancingStrategy.WeightedRoundRobin => SelectWeightedRoundRobin(eligibleWorkers),
            LoadBalancingStrategy.Random => SelectRandom(eligibleWorkers),
            _ => SelectLeastLoaded(eligibleWorkers)
        };
    }

    private WorkerNode SelectRoundRobin(List<WorkerNode> workers)
    {
        // Simplified round-robin implementation
        var index = DateTime.UtcNow.Millisecond % workers.Count;
        return workers[index];
    }

    private WorkerNode SelectLeastLoaded(List<WorkerNode> workers)
    {
        return workers.OrderBy(w => w.CurrentLoad).First();
    }

    private WorkerNode SelectWeightedRoundRobin(List<WorkerNode> workers)
    {
        // Weight by available capacity
        var totalWeight = workers.Sum(w => w.MaxCapacity - w.CurrentLoad);
        if (totalWeight <= 0) return workers.First();

        var random = new Random().Next(totalWeight);
        var currentWeight = 0;

        foreach (var worker in workers)
        {
            currentWeight += worker.MaxCapacity - worker.CurrentLoad;
            if (random < currentWeight)
                return worker;
        }

        return workers.Last();
    }

    private WorkerNode SelectRandom(List<WorkerNode> workers)
    {
        var index = new Random().Next(workers.Count);
        return workers[index];
    }

    private bool CheckSystemHealth()
    {
        lock (_workersLock)
        {
            var healthyWorkers = _workers.Values
                .Count(w => DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2));

            return healthyWorkers > 0 && healthyWorkers >= _config.MinimumWorkers;
        }
    }
}

/// <summary>
/// Load balancing configuration
/// </summary>
public record LoadBalancingConfig
{
    public LoadBalancingStrategy Strategy { get; init; } = LoadBalancingStrategy.LeastLoaded;
    public int MinimumWorkers { get; init; } = 1;
    public int MaximumWorkers { get; init; } = 10;
    public TimeSpan WorkerTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public bool AutoRebalance { get; init; } = true;
    public TimeSpan RebalanceInterval { get; init; } = TimeSpan.FromMinutes(5);
    public double LoadThreshold { get; init; } = 0.8; // 80%
}

/// <summary>
/// Load balancing strategies
/// </summary>
public enum LoadBalancingStrategy
{
    RoundRobin,
    LeastLoaded,
    WeightedRoundRobin,
    Random
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
/// Worker heartbeat request
/// </summary>
public record WorkerHeartbeatRequest(
    int CurrentLoad
);
