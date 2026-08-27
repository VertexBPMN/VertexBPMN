using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;

namespace VertexBPMN.Engine.Execution;

internal static class BpmnConditionEvaluator
{
    public static bool Evaluate(
        string rawExpression,
        IEnumerable<KeyValuePair<string, object>> variables)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawExpression);

        var expression = rawExpression.Trim();
        if ((expression.StartsWith("${", StringComparison.Ordinal)
             || expression.StartsWith("#{", StringComparison.Ordinal))
            && expression.EndsWith('}'))
        {
            expression = expression[2..^1].Trim();
        }

        expression = Regex.Replace(
            expression,
            @"(?<![<>=!])=(?!=)",
            "==",
            RegexOptions.CultureInvariant);
        expression = Regex.Replace(
            expression,
            @"\band\b",
            "&&",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        expression = Regex.Replace(
            expression,
            @"\bor\b",
            "||",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        expression = Regex.Replace(
            expression,
            @"\bnot\s+",
            "!",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var engine = new Jint.Engine(options => options
            .TimeoutInterval(TimeSpan.FromMilliseconds(100))
            .LimitRecursion(64)
            .MaxStatements(1_000));
        foreach (var variable in variables)
            engine.SetValue(variable.Key, NormalizeJsonValue(variable.Value));

        return engine.Evaluate(expression).AsBoolean();
    }

    public static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement json) return value;
        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number when json.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => json.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => JsonSerializer.Deserialize<object>(json.GetRawText())
        };
    }
}
