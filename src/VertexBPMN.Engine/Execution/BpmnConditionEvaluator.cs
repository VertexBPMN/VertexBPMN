using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
using Jint.Runtime;
using VertexBPMN.Application;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Execution;

internal static class BpmnConditionEvaluator
{
    public static bool Evaluate(
        BpmnSequenceFlow flow,
        IEnumerable<KeyValuePair<string, object>> variables)
    {
        ArgumentNullException.ThrowIfNull(flow);
        return Evaluate(flow.ConditionExpression!, variables, flow.ConditionExpressionLanguage);
    }

    public static bool Evaluate(
        string rawExpression,
        IEnumerable<KeyValuePair<string, object>> variables,
        string? expressionLanguage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawExpression);

        var expression = rawExpression.Trim();
        if ((expression.StartsWith("${", StringComparison.Ordinal)
             || expression.StartsWith("#{", StringComparison.Ordinal))
            && expression.EndsWith('}'))
        {
            expression = expression[2..^1].Trim();
        }

        var context = variables.ToDictionary(
            variable => variable.Key,
            variable => NormalizeJsonValue(variable.Value)!,
            StringComparer.Ordinal);

        // Several BPMN 2.0 interchange fixtures declare XPath globally but use
        // Signavio's data-object accessor syntax in the actual formal expression.
        // Resolve that accessor against the process-variable context before the
        // FEEL / fallback split, so both the FEEL runtime and the compatibility
        // evaluator see it as a plain variable reference.
        expression = Regex.Replace(
            expression,
            "(?:[A-Za-z_][\\w.-]*:)?getDataObject\\(\\s*(['\"])(?<name>[^'\"]+)\\1\\s*\\)",
            match => match.Groups["name"].Value,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (IsFeel(expressionLanguage, expression))
        {
            if (expression.StartsWith('=') && !expression.StartsWith("==", StringComparison.Ordinal))
                expression = expression[1..].Trim();

            // BPMN 2.0 interchange conditions are commonly XPath-flavoured and quote
            // string literals with single quotes, whereas FEEL strings require double
            // quotes. Normalize single-quoted literals so such conditions evaluate in
            // the FEEL runtime instead of failing on an unrecognized token.
            if (expression.Contains('\'') && !expression.Contains('\"'))
            {
                expression = Regex.Replace(
                    expression,
                    "'(?<lit>[^']*)'",
                    match => "\"" + match.Groups["lit"].Value + "\"");
            }

            var result = FeelExpressionRuntime.Evaluate(expression, context);
            return result is bool boolean
                ? boolean
                : throw new InvalidOperationException(
                    $"FEEL condition '{rawExpression}' returned '{result?.GetType().Name ?? "null"}' instead of boolean.");
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
        foreach (var variable in context)
            engine.SetValue(variable.Key, variable.Value);

        try
        {
            return engine.Evaluate(expression).AsBoolean();
        }
        catch (JavaScriptException exception)
        {
            throw new InvalidOperationException(
                $"BPMN condition '{rawExpression}' could not be evaluated with the available process variables.",
                exception);
        }
    }

    private static bool IsFeel(string? expressionLanguage, string expression) =>
        expression.StartsWith('=')
        || expressionLanguage?.Contains("FEEL", StringComparison.OrdinalIgnoreCase) == true
        || (string.IsNullOrWhiteSpace(expressionLanguage)
            && Regex.IsMatch(
                expression,
                @"(?<![<>=!])\s=\s(?![=])",
                RegexOptions.CultureInvariant));

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
