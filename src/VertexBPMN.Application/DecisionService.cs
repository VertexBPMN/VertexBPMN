using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

/// <summary>
/// DMN 1.4 DecisionService mit echtem Decision Table Parsing/Evaluation.
/// Supports full decision lifecycle: deployment, evaluation, and instance tracking.
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

            // Parse and validate DMN XML
            var decisionTable = DmnDecisionTable.Parse(dmnXml);

            ValidateDecisionTable(decisionTable);

            var definition = new DecisionDefinition(decisionKey, name, dmnXml, tenantId, decisionTable);
            await _repository.UpsertDefinitionAsync(definition);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Successfully deployed decision {DecisionKey} with {InputCount} inputs and {RuleCount} rules",
                decisionKey, decisionTable.Inputs.Count, decisionTable.Rules.Count);
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
        if (definition.DecisionTable == null)
        {
            _logger.LogWarning("Decision {DecisionKey} has no decision table", definition.Key);
            return new DecisionResult(variables);
        }

        var readonlyVariables = variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        var result = definition.DecisionTable.Evaluate(readonlyVariables);
        return new DecisionResult(result);
    }

    private static void ValidateDecisionTable(DmnDecisionTable table)
    {
        if (table.Inputs.Count == 0)
            throw new InvalidOperationException($"Decision table '{table.Key}' must have at least one input");
        if (table.Outputs.Count == 0)
            throw new InvalidOperationException($"Decision table '{table.Key}' must have at least one output");
        if (table.Rules.Count == 0)
            throw new InvalidOperationException($"Decision table '{table.Key}' must have at least one rule");
    }
}
