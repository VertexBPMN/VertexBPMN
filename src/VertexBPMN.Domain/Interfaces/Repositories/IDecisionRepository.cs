
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for DMN decision definitions and evaluation history.
/// </summary>
public interface IDecisionRepository
{
    // Definitions
    ValueTask UpsertDefinitionAsync(DecisionDefinition definition, CancellationToken ct = default);
    ValueTask<DecisionDefinition?> GetDefinitionAsync(string key, string? tenantId = null, CancellationToken ct = default);
    IAsyncEnumerable<DecisionDefinition> ListDefinitionsAsync(string? key = null, string? tenantId = null, CancellationToken ct = default);

    // Instances
    ValueTask AddInstanceAsync(DecisionInstance instance, CancellationToken ct = default);
    IAsyncEnumerable<DecisionInstance> ListInstancesAsync(string? decisionKey = null, string? tenantId = null, CancellationToken ct = default);

    // Flush (Unit of Work)
    ValueTask SaveChangesAsync(CancellationToken ct = default);
}
