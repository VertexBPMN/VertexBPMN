//using VertexBPMN.Domain.Entities;

using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Domain.Interfaces
{
    /// <summary>
    /// Provides operations for evaluating DMN decisions and managing decision resources.
    /// </summary>
    public interface IDecisionService
    {
        // Vertex-kompatible Decision Definition API
        IAsyncEnumerable<DecisionDefinition> ListAsync(string? key = null, string? tenantId = null, CancellationToken cancellationToken = default);

        // Vertex-kompatible Decision Instance API
        IAsyncEnumerable<DecisionInstance> ListInstancesAsync(string? decisionKey = null, string? tenantId = null, CancellationToken cancellationToken = default);

        // Decision evaluation and retrieval
        ValueTask<DecisionResult> EvaluateDecisionByKeyAsync(string decisionKey, IDictionary<string, object> variables, string? tenantId = null, CancellationToken cancellationToken = default);
        ValueTask<DecisionDefinition?> GetDecisionByKeyAsync(string decisionKey, string? tenantId = null, CancellationToken cancellationToken = default);
        
        // Decision deployment
        ValueTask DeployAsync(string decisionKey, string name, string dmnXml, string? tenantId = null);
    }
}
