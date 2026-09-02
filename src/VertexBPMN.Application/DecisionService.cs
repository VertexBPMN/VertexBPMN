using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Domain.Model.Dmn;


namespace VertexBPMN.Application;

/// <summary>
/// Persistent lifecycle for qualified DMN decision tables, literal expressions,
/// decision-requirements graphs and decision services.
/// </summary>
public class DecisionService : IDecisionService
{
    private readonly ILogger<DecisionService> _logger;
    private readonly IDecisionRepository _repository;

    public DecisionService(ILogger<DecisionService> logger, IDecisionRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async IAsyncEnumerable<DecisionDefinition> ListAsync(string? key = null, string? tenantId = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var def in _repository.ListDefinitionsAsync(key, tenantId, cancellationToken))
            yield return def;
    }

    public async IAsyncEnumerable<DecisionInstance> ListInstancesAsync(string? decisionKey = null, string? tenantId = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var inst in _repository.ListInstancesAsync(decisionKey, tenantId, cancellationToken))
            yield return inst;
    }

    public async ValueTask<DecisionResult> EvaluateDecisionByKeyAsync(string decisionKey, IDictionary<string, object> variables,
        string? tenantId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionKey);
        ArgumentNullException.ThrowIfNull(variables);

        var evaluationId = Guid.NewGuid().ToString();
        var evaluationTime = DateTime.UtcNow;

        try
        {
            var definition = await _repository.GetDefinitionAsync(decisionKey, tenantId, cancellationToken);
            if (definition == null)
            {
                var msg = $"Decision definition '{decisionKey}' not found for tenant '{tenantId ?? "default"}'";
                _logger.LogWarning(msg);
                await RecordInstanceAsync(new DecisionInstance(
                    evaluationId, decisionKey, tenantId, evaluationTime,
                    variables.ToDictionary(kv => kv.Key, kv => kv.Value),
                    new Dictionary<string, object>(), true, msg), cancellationToken);
                throw new KeyNotFoundException(msg);
            }

            var result = EvaluateDecisionTable(definition, variables);

            await RecordInstanceAsync(new DecisionInstance(
                evaluationId, decisionKey, tenantId, evaluationTime,
                variables.ToDictionary(kv => kv.Key, kv => kv.Value),
                result.Variables, false), cancellationToken);

            return result;
        }
        catch (Exception ex) when (ex is not KeyNotFoundException)
        {
            _logger.LogError(ex, "Error evaluating decision {DecisionKey}", decisionKey);
            await RecordInstanceAsync(new DecisionInstance(
                evaluationId, decisionKey, tenantId, evaluationTime,
                variables.ToDictionary(kv => kv.Key, kv => kv.Value),
                new Dictionary<string, object>(), true, ex.Message), cancellationToken);
            throw;
        }
    }

    public async ValueTask<DecisionDefinition?> GetDecisionByKeyAsync(string decisionKey, string? tenantId = null,
        CancellationToken cancellationToken = default)
        => await _repository.GetDefinitionAsync(decisionKey, tenantId, cancellationToken);

    public async ValueTask DeployAsync(string decisionKey, string name, string dmnXml, string? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(dmnXml);

        try
        {
            _logger.LogDebug("Deploying decision {DecisionKey} for tenant {TenantId}", decisionKey, tenantId ?? "default");

            _ = DmnDecisionGraph.Parse(dmnXml, decisionKey);
            var definition = new DecisionDefinition(decisionKey, name, dmnXml, tenantId);
            await _repository.UpsertDefinitionAsync(definition);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Successfully deployed validated DMN decision graph {DecisionKey}", decisionKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy decision {DecisionKey}", decisionKey);
            throw;
        }
    }

    private static async ValueTask RecordInstanceAsync(DecisionInstance instance, CancellationToken ct, IDecisionRepository? repo = null)
    {
        if (repo != null)
        {
            await repo.AddInstanceAsync(instance, ct);
            await repo.SaveChangesAsync(ct);
        }
    }

    private async ValueTask RecordInstanceAsync(DecisionInstance instance, CancellationToken ct)
        => await RecordInstanceAsync(instance, ct, _repository);

    private DecisionResult EvaluateDecisionTable(DecisionDefinition definition, IDictionary<string, object> variables)
    {
        var result = DmnDecisionGraph.Parse(definition.DmnXml, definition.Key).Evaluate(variables);
        return new DecisionResult(result);
    }

}
