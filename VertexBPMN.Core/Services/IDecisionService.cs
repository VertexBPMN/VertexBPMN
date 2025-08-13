
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VertexBPMN.Core.Services
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

        // Vorhandene Methoden
        ValueTask<DecisionResult> EvaluateDecisionByKeyAsync(string decisionKey, IDictionary<string, object> variables, string? tenantId = null, CancellationToken cancellationToken = default);
        ValueTask<DecisionDefinition?> GetDecisionByKeyAsync(string decisionKey, string? tenantId = null, CancellationToken cancellationToken = default);
    }

    public record DecisionInstance(string Id, string DecisionKey, object? Result, DateTime EvaluatedAt, string? TenantId);

    /// <summary>
    /// Represents the result of a DMN decision evaluation.
    /// </summary>
    public record DecisionResult(IDictionary<string, object> Outputs);

    /// <summary>
    /// Represents a DMN decision definition.
    /// </summary>
    public record DecisionDefinition(string Key, string Name, string DmnXml, string? TenantId);
}
