namespace VertexBPMN.Core.Engine;

/// <summary>
/// Distributed token execution engine for enterprise scalability
/// Olympic-level feature: Enterprise Scalability - Distributed processing
/// </summary>
public interface IDistributedTokenEngine
{
    Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default);
    Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default);
    Task DistributeTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default);
    Task<List<ExecutionToken>> GetPendingTokensAsync(CancellationToken cancellationToken = default);
}