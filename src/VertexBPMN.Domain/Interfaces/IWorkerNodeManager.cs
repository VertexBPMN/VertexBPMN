using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Worker node management interface for distributed execution
/// Olympic-level feature: Enterprise Scalability - Worker Management
/// </summary>
public interface IWorkerNodeManager
{
    Task<WorkerNode> RegisterWorkerAsync(WorkerRegistrationRequest request);
    System.Threading.Tasks.Task UnregisterWorkerAsync(string workerId);
    Task<List<WorkerNode>> GetActiveWorkersAsync();
    Task<WorkerNode> GetWorkerAsync(string workerId);
    System.Threading.Tasks.Task UpdateWorkerStatusAsync(string workerId, WorkerStatus status);
    Task<WorkerCapacityInfo> GetWorkerCapacityAsync(string workerId);
    Task<bool> IsWorkerHealthyAsync(string workerId);
    Task<List<WorkerNode>> GetWorkersForNodeTypeAsync(string nodeType);
    System.Threading.Tasks.Task NotifyWorkersAsync(string message);
    Task<WorkerPerformanceMetrics> GetWorkerPerformanceAsync(string workerId);
}