using System.Text.Json;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Studio.Services;

public interface IDmnService
{
    Task DeployAsync(string decisionKey, string name, string dmnXml, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<DecisionDefinition?> GetByKeyAsync(string decisionKey, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<DecisionResult> EvaluateAsync(string decisionKey, IDictionary<string, object> inputs, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<JsonElement> ListDefinitionsAsync(string? key = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<JsonElement> ListInstancesAsync(string? decisionKey = null, string? tenantId = null, CancellationToken cancellationToken = default);
}
