using Jint;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Engine.Execution;

/// <summary>
/// Evaluates DMN decisions based on input variables.
/// </summary>
public class DmnEngine : IDmnEngine
{
    private readonly ILogger<DmnEngine> _logger;

    public DmnEngine(ILogger<DmnEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Evaluates a DMN decision and returns the output values based on the hit policy.
    /// </summary>
    /// <param name="decision">The DMN decision to evaluate.</param>
    /// <param name="variables">Input variables for the decision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of output values.</returns>
    public async Task<Dictionary<string, object>> EvaluateDecisionAsync(DmnDecision decision, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        try
        {
            var matchingRules = new List<DmnRule>();

            foreach (var rule in decision.Rules)
            {
                bool allConditionsMet = await EvaluateRuleConditionsAsync(rule, decision.Inputs, variables, cancellationToken);
                if (allConditionsMet)
                {
                    matchingRules.Add(rule);
                    if (decision.HitPolicy == "FIRST")
                        break; // Stop after first matching rule for FIRST hit policy
                }
            }

            if (!matchingRules.Any())
                throw new DmnEvaluationException($"No rule matched for decision {decision.Id}");

            var result = decision.HitPolicy switch
            {
                "UNIQUE" => HandleUniqueHitPolicy(decision, matchingRules),
                "FIRST" => HandleFirstHitPolicy(matchingRules),
                "PRIORITY" => HandlePriorityHitPolicy(decision, matchingRules),
                "COLLECT" => HandleCollectHitPolicy(decision, matchingRules),
                _ => throw new DmnEvaluationException($"Unsupported hit policy: {decision.HitPolicy}")
            };

            _logger.LogInformation("Evaluated decision {DecisionId} with hit policy {HitPolicy}, matched {RuleCount} rules", decision.Id, decision.HitPolicy, matchingRules.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate decision {DecisionId}", decision.Id);
            throw new DmnEvaluationException($"Failed to evaluate decision {decision.Id}", ex);
        }
    }

    private async Task<bool> EvaluateRuleConditionsAsync(DmnRule rule, IReadOnlyList<DmnInput> inputs, Dictionary<string, object> variables, CancellationToken cancellationToken)
    {
        foreach (var input in inputs)
        {
            if (!rule.InputConditions.TryGetValue(input.Id, out var condition) || condition == "-")
                continue; // "-" means "any value"

            if (!variables.TryGetValue(input.Label, out var variableValue))
                return false;

            bool conditionMet = await Task.Run(() => EvaluateCondition(condition, variableValue, input.TypeRef), cancellationToken);
            if (!conditionMet)
                return false;
        }
        return true;
    }

    private bool EvaluateCondition(string condition, object variableValue, string typeRef)
    {
        try
        {
            var engine = new Jint.Engine();
            engine.SetValue("input", ConvertValue(variableValue, typeRef));

            // Handle FEEL-like expressions
            string cleanedCondition = condition switch
            {
                var c when c.StartsWith(">=") => $"input >= {c.Substring(2)}",
                var c when c.StartsWith("<=") => $"input <= {c.Substring(2)}",
                var c when c.StartsWith(">") => $"input > {c.Substring(1)}",
                var c when c.StartsWith("<") => $"input < {c.Substring(1)}",
                var c when c.StartsWith("=") => $"input == {c.Substring(1)}",
                var c when c.StartsWith("!=") => $"input != {c.Substring(2)}",
                _ => $"input == {condition}" // Default to equality for strings or simple values
            };

            return engine.Evaluate(cleanedCondition).AsBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate condition: {Condition} for value {Value}", condition, variableValue);
            return false;
        }
    }

    private object ConvertValue(object value, string typeRef)
    {
        return typeRef.ToLower() switch
        {
            "integer" => Convert.ToInt32(value),
            "double" => Convert.ToDouble(value),
            "boolean" => Convert.ToBoolean(value),
            "string" => value?.ToString(),
            _ => value
        };
    }

    private Dictionary<string, object> HandleUniqueHitPolicy(DmnDecision decision, List<DmnRule> matchingRules)
    {
        if (matchingRules.Count > 1)
            throw new DmnEvaluationException($"UNIQUE hit policy requires exactly one matching rule, but {matchingRules.Count} rules matched for decision {decision.Id}");
        return matchingRules.First().OutputValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private Dictionary<string, object> HandleFirstHitPolicy(List<DmnRule> matchingRules)
    {
        return matchingRules.First().OutputValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private Dictionary<string, object> HandlePriorityHitPolicy(DmnDecision decision, List<DmnRule> matchingRules)
    {
        if (!matchingRules.Any())
            throw new DmnEvaluationException($"No matching rules for PRIORITY hit policy in decision {decision.Id}");

        var outputId = decision.Outputs.First().Id; // Assume single output for simplicity
        var orderedRules = matchingRules.OrderByDescending(rule =>
        {
            if (rule.OutputValues.TryGetValue(outputId, out var value))
            {
                return decision.Outputs.First().TypeRef switch
                {
                    "integer" => int.TryParse(value.ToString(), out var intVal) ? intVal : 0,
                    "double" => double.TryParse(value.ToString(), out var doubleVal) ? doubleVal : 0,
                    _ => value.ToString()?.Length ?? 0 // Fallback for string comparison
                };
            }
            return 0;
        });

        return orderedRules.First().OutputValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private Dictionary<string, object> HandleCollectHitPolicy(DmnDecision decision, List<DmnRule> matchingRules)
    {
        var result = new Dictionary<string, object>();
        foreach (var output in decision.Outputs)
        {
            var values = matchingRules.Select(rule => rule.OutputValues.GetValueOrDefault(output.Id)).ToList();
            result[output.Id] = values; // Return list of values for each output
        }
        return result;
    }
}
