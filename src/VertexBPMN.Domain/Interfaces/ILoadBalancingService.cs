using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

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