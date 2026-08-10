using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Model.Cmn;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Extended interface for distributed/enterprise features.
/// Only distributed engines implement this interface to provide advanced capabilities.
/// Check 'engine is IDistributedProcessEngine' at runtime to access these features.
/// </summary>
public interface IDistributedProcessEngine : IProcessEngine
{
    #region Token Management
    
    /// <summary>
    /// Distributes an execution token to available worker nodes.
    /// </summary>
    /// <param name="token">Token to distribute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DistributeTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Distributes a case token to available worker nodes.
    /// </summary>
    /// <param name="token">Case token to distribute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DistributeCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets pending execution tokens waiting for processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending tokens</returns>
    Task<List<ExecutionToken>> GetPendingTokensAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets pending case tokens waiting for processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending case tokens</returns>
    Task<List<CaseToken>> GetPendingCaseTokensAsync(CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Worker Management
    
    /// <summary>
    /// Registers a worker node in the distributed engine cluster.
    /// </summary>
    /// <param name="worker">Worker node to register</param>
    Task RegisterWorkerAsync(WorkerNode worker);
    
    /// <summary>
    /// Unregisters a worker node from the distributed engine cluster.
    /// </summary>
    /// <param name="workerId">Worker ID to unregister</param>
    Task UnregisterWorkerAsync(string workerId);
    
    /// <summary>
    /// Updates worker heartbeat to keep it alive in the cluster.
    /// </summary>
    /// <param name="workerId">Worker ID</param>
    Task UpdateWorkerHeartbeatAsync(string workerId);
    
    #endregion
    
}