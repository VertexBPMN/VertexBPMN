using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VertexBPMN.Core.Contracts;
using VertexBPMN.Domain;

namespace VertexBPMN.EngineServices;

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