using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Modeling;

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
    
    #region Advanced CMMN Features
    
    /// <summary>
    /// Adds a discretionary item to a running case instance.
    /// Enables dynamic case evolution during execution.
    /// </summary>
    /// <param name="caseId">Case ID</param>
    /// <param name="planItem">Plan item to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddDiscretionaryItemAsync(string caseId, PlanItem planItem, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a case file item value and triggers dependent sentries.
    /// Core CMMN functionality for data-driven case execution.
    /// </summary>
    /// <param name="caseId">Case ID</param>
    /// <param name="caseFileItemId">Case file item ID</param>
    /// <param name="newValue">New value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateCaseFileItemAsync(string caseId, string caseFileItemId, object newValue, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Triggers a user-defined event in a case instance.
    /// Allows external systems to influence case execution.
    /// </summary>
    /// <param name="caseId">Case ID</param>
    /// <param name="eventId">Event ID</param>
    /// <param name="eventData">Event data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TriggerUserEventAsync(string caseId, string eventId, Dictionary<string, object> eventData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates an ad-hoc subprocess using AI-powered decision making.
    /// Olympic-level feature: AI-Enhanced Process Optimization.
    /// </summary>
    /// <param name="caseId">Case ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task GenerateAdHocSubprocessAsync(string caseId, CancellationToken cancellationToken = default);
    
    #endregion
}