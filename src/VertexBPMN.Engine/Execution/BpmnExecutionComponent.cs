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
