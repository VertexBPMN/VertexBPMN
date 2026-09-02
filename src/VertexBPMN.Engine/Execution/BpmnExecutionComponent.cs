using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Execution;

/// <summary>
/// Shared, side-effect-free BPMN execution decisions.
/// Token ownership, persistence, retries, and trace formatting remain engine-specific.
/// </summary>
public sealed class BpmnExecutionComponent
{
    public IReadOnlyList<BpmnSequenceFlow> GetOutgoingFlows(BpmnModel model, string sourceId)
        => model.SequenceFlows.Where(flow => flow.SourceRef == sourceId).ToList();

    public GatewayDecision SelectExclusiveFlow( IEnumerable<BpmnSequenceFlow> flows,
        IDictionary<string, object> variables,
        Func<BpmnSequenceFlow, IDictionary<string, object>, bool> conditionEvaluator)
    {
        var ordered = flows.ToList();

        var defaultFlows = ordered
            .Where(flow => flow.IsDefault)
            .ToList();

        if (defaultFlows.Count > 1)
        {
            throw new InvalidOperationException(
                "An Exclusive Gateway may have at most one default SequenceFlow.");
        }

        var defaultFlow = defaultFlows.SingleOrDefault();

        // Default-Flows werden nicht als normale Conditions ausgewertet.
        foreach (var flow in ordered.Where(flow => !flow.IsDefault))
        {
            if (MatchesCondition(flow, variables, conditionEvaluator))
            {
                return new GatewayDecision(
                    GatewayDecisionKind.Selected,
                    flow);
            }
        }

        if (defaultFlow != null)
        {
            return new GatewayDecision(
                GatewayDecisionKind.DefaultSelected,
                defaultFlow);
        }

        // Niemals auf den ersten beliebigen Flow zurückfallen.
        return new GatewayDecision(
            GatewayDecisionKind.NoOutgoingFlow,
            null);
    }

    public IReadOnlyList<BpmnSequenceFlow> SelectInclusiveFlows(
        IEnumerable<BpmnSequenceFlow> flows,
        IDictionary<string, object> variables,
        Func<BpmnSequenceFlow, IDictionary<string, object>, bool> conditionEvaluator)
    {
        var ordered = flows.ToList();

        var defaultFlows = ordered
            .Where(flow => flow.IsDefault)
            .ToList();

        if (defaultFlows.Count > 1)
        {
            throw new InvalidOperationException(
                "An Inclusive Gateway may have at most one default SequenceFlow.");
        }

        var matchingFlows = ordered
            .Where(flow => !flow.IsDefault)
            .Where(flow => MatchesCondition(flow, variables, conditionEvaluator))
            .ToList();

        // Reguläre Treffer haben Vorrang vor dem Default-Flow.
        if (matchingFlows.Count > 0)
        {
            return matchingFlows;
        }

        var defaultFlow = defaultFlows.SingleOrDefault();

        return defaultFlow == null
            ? Array.Empty<BpmnSequenceFlow>()
            : new[] { defaultFlow };
    }

    private static bool MatchesCondition(
        BpmnSequenceFlow flow,
        IDictionary<string, object> variables,
        Func<BpmnSequenceFlow, IDictionary<string, object>, bool> conditionEvaluator)
    {
        // Kein conditionExpression bedeutet: Flow ist zulässig.
        if (string.IsNullOrWhiteSpace(flow.ConditionExpression))
        {
            return true;
        }

        return conditionEvaluator(flow, variables);
    }
    public BpmnSequenceFlow? SelectExclusiveFlow(
        IEnumerable<BpmnSequenceFlow> flows,
        IDictionary<string, object> variables,
        Func<string, IDictionary<string, object>, bool> conditionEvaluator,
        Action<string>? onConditionMatched = null,
        Action<string>? onDefaultTaken = null,
        Action<string>? onFallback = null)
    {
        var ordered = flows
            .Select(flow => (Flow: flow, Priority: flow.Priority ?? int.MaxValue))
            .OrderBy(item => item.Priority)
            .Select(item => item.Flow)
            .ToList();
        var defaultFlow = ordered.FirstOrDefault(flow => flow.IsDefault);

        foreach (var flow in ordered)
        {
            var condition = GetConditionExpression(flow);
            if (!string.IsNullOrWhiteSpace(condition) && conditionEvaluator(condition, variables))
            {
                onConditionMatched?.Invoke(flow.Id);
                return flow;
            }
        }

        if (defaultFlow != null)
        {
            onDefaultTaken?.Invoke(defaultFlow.Id);
            return defaultFlow;
        }

        var fallback = ordered.FirstOrDefault();
        if (fallback != null)
            onFallback?.Invoke(fallback.Id);
        return fallback;
    }

    public IReadOnlyList<BpmnSequenceFlow> SelectMatchingFlows(
        IEnumerable<BpmnSequenceFlow> flows,
        IDictionary<string, object> variables,
        Func<string, IDictionary<string, object>, bool> conditionEvaluator)
        => flows.Where(flow =>
        {
            var condition = GetConditionExpression(flow);
            return string.IsNullOrWhiteSpace(condition) || conditionEvaluator(condition, variables);
        }).ToList();

    public IReadOnlyList<BpmnSequenceFlow> SelectFirstMatchingFlow(
        IEnumerable<BpmnSequenceFlow> flows,
        IDictionary<string, object> variables,
        Func<string, IDictionary<string, object>, bool> conditionEvaluator)
    {
        var selected = flows.FirstOrDefault(flow =>
        {
            var condition = GetConditionExpression(flow);
            return string.IsNullOrWhiteSpace(condition) || conditionEvaluator(condition, variables);
        });
        return selected == null ? [] : [selected];
    }

    public bool EvaluateSimpleCondition(string rawExpression, IDictionary<string, object> variables)
    {
        if (string.IsNullOrWhiteSpace(rawExpression)) return false;
        var expression = rawExpression.Trim();
        if (expression.StartsWith("${") && expression.EndsWith("}"))
            expression = expression[2..^1].Trim();
        if (!expression.Contains("==", StringComparison.Ordinal) &&
            expression.Contains('=') && !expression.Contains("!=", StringComparison.Ordinal))
        {
            var index = expression.IndexOf('=');
            expression = expression[..index] + "==" + expression[(index + 1)..];
        }

        var equalityIndex = expression.IndexOf("==", StringComparison.Ordinal);
        if (equalityIndex > 0)
            return Compare(expression, equalityIndex, 2, variables, false);
        var inequalityIndex = expression.IndexOf("!=", StringComparison.Ordinal);
        if (inequalityIndex > 0)
            return Compare(expression, inequalityIndex, 2, variables, true);
        return variables.ContainsKey(expression);
    }

    public static string? GetConditionExpression(BpmnSequenceFlow flow)
        => flow.ConditionExpression;

    private static bool Compare(string expression, int operatorIndex, int operatorLength,
        IDictionary<string, object> variables, bool negate)
    {
        var variableName = expression[..operatorIndex].Trim();
        var literal = TrimQuotes(expression[(operatorIndex + operatorLength)..].Trim());
        if (!variables.TryGetValue(variableName, out var value)) return false;
        var equal = string.Equals(value?.ToString(), literal, StringComparison.OrdinalIgnoreCase);
        return negate ? !equal : equal;
    }

    private static string TrimQuotes(string value)
        => value.Length >= 2 &&
           ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;
}
