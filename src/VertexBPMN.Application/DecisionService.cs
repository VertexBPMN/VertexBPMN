using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>
/// In-memory DMN 1.4 DecisionService mit echtem Decision Table Parsing/Evaluation.
/// Supports full decision lifecycle: deployment, evaluation, and instance tracking.
/// </summary>
public class DecisionService : IDecisionService
{
    private readonly ILogger<DecisionService> _logger;
    private readonly ConcurrentDictionary<string, DecisionDefinition> _decisions = new();
    private readonly ConcurrentDictionary<string, List<DecisionInstance>> _instances = new();
    private readonly object _lockObject = new();

    public DecisionService(ILogger<DecisionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lists all deployed decision definitions with optional filtering.
    /// </summary>
    public async IAsyncEnumerable<DecisionDefinition> ListAsync(string? key = null, string? tenantId = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Make it truly async

        var filteredDecisions = _decisions.Values
            .Where(d => (key == null || d.Key == key) &&
                       (tenantId == null || d.TenantId == tenantId))
            .OrderBy(d => d.Key);

        foreach (var decision in filteredDecisions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return decision;
        }
    }

    /// <summary>
    /// Lists decision instances (evaluation history) with optional filtering.
    /// </summary>
    public async IAsyncEnumerable<DecisionInstance> ListInstancesAsync(string? decisionKey = null, string? tenantId = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Make it truly async

        var allInstances = new List<DecisionInstance>();

        lock (_lockObject)
        {
            foreach (var instanceList in _instances.Values)
            {
                allInstances.AddRange(instanceList);
            }
        }

        var filteredInstances = allInstances
            .Where(i => (decisionKey == null || i.DecisionDefinitionKey == decisionKey) &&
                       (tenantId == null || i.TenantId == tenantId))
            .OrderByDescending(i => i.EvaluationTime);

        foreach (var instance in filteredInstances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return instance;
        }
    }

    /// <summary>
    /// Evaluates a decision by key with comprehensive error handling and instance tracking.
    /// </summary>
    public ValueTask<DecisionResult> EvaluateDecisionByKeyAsync(string decisionKey, IDictionary<string, object> variables,
        string? tenantId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionKey);
        ArgumentNullException.ThrowIfNull(variables);

        var evaluationId = Guid.NewGuid().ToString();
        var evaluationTime = DateTime.UtcNow;

        try
        {
            _logger.LogDebug("Evaluating decision {DecisionKey} with {VariableCount} variables",
                decisionKey, variables.Count);

            if (!_decisions.TryGetValue(GenerateDecisionKey(decisionKey, tenantId), out var definition))
            {
                var errorMessage = $"Decision definition '{decisionKey}' not found for tenant '{tenantId ?? "default"}'";
                _logger.LogWarning(errorMessage);

                // Create failed instance
                var failedInstance = new DecisionInstance(
                    evaluationId, decisionKey, tenantId, evaluationTime,
                    variables.ToDictionary(kv => kv.Key, kv => kv.Value),
                    new Dictionary<string, object>(),
                    Failed: true, errorMessage);

                RecordDecisionInstance(decisionKey, failedInstance);

                throw new KeyNotFoundException(errorMessage);
            }

            var result = EvaluateDecisionTable(definition, variables);

            // Create successful instance
            var instance = new DecisionInstance(
                evaluationId, decisionKey, tenantId, evaluationTime,
                variables.ToDictionary(kv => kv.Key, kv => kv.Value),
                result.Variables,
                Failed: false);

            RecordDecisionInstance(decisionKey, instance);

            _logger.LogInformation("Successfully evaluated decision {DecisionKey} with {OutputCount} outputs",
                decisionKey, result.Variables.Count);

            return ValueTask.FromResult(result);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException)
        {
            _logger.LogError(ex, "Error evaluating decision {DecisionKey}", decisionKey);

            // Create failed instance for unexpected errors
            var failedInstance = new DecisionInstance(
                evaluationId, decisionKey, tenantId, evaluationTime,
                variables.ToDictionary(kv => kv.Key, kv => kv.Value),
                new Dictionary<string, object>(),
                Failed: true, ex.Message);

            RecordDecisionInstance(decisionKey, failedInstance);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific decision definition by key and tenant.
    /// </summary>
    public ValueTask<DecisionDefinition?> GetDecisionByKeyAsync(string decisionKey, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionKey);

        var key = GenerateDecisionKey(decisionKey, tenantId);
        _decisions.TryGetValue(key, out var definition);

        return ValueTask.FromResult(definition);
    }

    /// <summary>
    /// Deploys a new decision definition with comprehensive DMN parsing and validation.
    /// </summary>
    public ValueTask DeployAsync(string decisionKey, string name, string dmnXml, string? tenantId = null)
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

            var key = GenerateDecisionKey(decisionKey, tenantId);
            var definition = new DecisionDefinition(decisionKey, name, dmnXml, tenantId, decisionTable);

            _decisions.AddOrUpdate(key, definition, (_, _) => definition);

            _logger.LogInformation("Successfully deployed decision {DecisionKey} with {InputCount} inputs and {RuleCount} rules",
                decisionKey, decisionTable.Inputs.Count, decisionTable.Rules.Count);

            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy decision {DecisionKey}", decisionKey);
            throw;
        }
    }

    /// <summary>
    /// Evaluates the decision table with comprehensive rule matching and hit policy handling.
    /// </summary>
    private DecisionResult EvaluateDecisionTable(DecisionDefinition definition, IDictionary<string, object> variables)
    {
        if (definition.DecisionTable == null)
        {
            _logger.LogWarning("Decision {DecisionKey} has no decision table", definition.Key);
            return new DecisionResult(variables);
        }

        var table = definition.DecisionTable;

        _logger.LogDebug("Evaluating decision table {TableKey} with hit policy {HitPolicy}",
            table.Key, table.HitPolicy);

        // Convert variables to readonly dictionary for evaluation
        var readonlyVariables = variables.ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = table.Evaluate(readonlyVariables);

        _logger.LogDebug("Decision table evaluation produced {OutputCount} outputs", result.Count);

        return new DecisionResult(result);
    }

    /// <summary>
    /// Validates the decision table for common issues and DMN compliance.
    /// </summary>
    private static void ValidateDecisionTable(DmnDecisionTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.Inputs.Count == 0)
            throw new InvalidOperationException($"Decision table '{table.Key}' must have at least one input");

        if (table.Outputs.Count == 0)
            throw new InvalidOperationException($"Decision table '{table.Key}' must have at least one output");

        if (table.Rules.Count == 0)
            throw new InvalidOperationException($"Decision table '{table.Key}' must have at least one rule");

        // Validate hit policy
        var validHitPolicies = new[] { "UNIQUE", "FIRST", "ANY", "COLLECT", "RULE ORDER", "PRIORITY", "OUTPUT ORDER" };
        if (!validHitPolicies.Contains(table.HitPolicy.ToUpperInvariant()))
        {
            throw new InvalidOperationException($"Unsupported hit policy '{table.HitPolicy}' in decision table '{table.Key}'");
        }

        // Validate rule structure
        foreach (var rule in table.Rules)
        {
            if (rule.InputConditions.Count != table.Inputs.Count)
            {
                throw new InvalidOperationException(
                    $"Rule '{rule.Id}' has {rule.InputConditions.Count} input conditions but table has {table.Inputs.Count} inputs");
            }

            if (rule.OutputValues.Count != table.Outputs.Count)
            {
                throw new InvalidOperationException(
                    $"Rule '{rule.Id}' has {rule.OutputValues.Count} output values but table has {table.Outputs.Count} outputs");
            }
        }
    }

    /// <summary>
    /// Records a decision instance for audit and history tracking.
    /// </summary>
    private void RecordDecisionInstance(string decisionKey, DecisionInstance instance)
    {
        lock (_lockObject)
        {
            if (!_instances.TryGetValue(decisionKey, out var instanceList))
            {
                instanceList = new List<DecisionInstance>();
                _instances[decisionKey] = instanceList;
            }

            instanceList.Add(instance);

            // Keep only the last 1000 instances per decision to prevent memory bloat
            if (instanceList.Count > 1000)
            {
                instanceList.RemoveRange(0, instanceList.Count - 1000);
                _logger.LogDebug("Trimmed decision instances for {DecisionKey} to 1000 most recent", decisionKey);
            }
        }
    }

    /// <summary>
    /// Generates a unique key for tenant-aware decision storage.
    /// </summary>
    private static string GenerateDecisionKey(string decisionKey, string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId)
            ? decisionKey
            : $"{decisionKey}#{tenantId}";
    }
}

