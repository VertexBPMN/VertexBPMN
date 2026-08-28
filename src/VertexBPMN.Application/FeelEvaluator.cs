using System.Collections;
using System.Globalization;
using System.Text.Json;
using FeelSharp;

namespace VertexBPMN.Application;

/// <summary>
/// Single FEEL execution boundary used by DMN literal expressions, input clauses,
/// output entries and decision-table unary tests.
/// </summary>
internal static class FeelEvaluator
{
    private static readonly IFeelEngine Engine = FeelEngine.Create();

    public static object? EvaluateExpression(
        string expression,
        IReadOnlyDictionary<string, object> context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var result = Engine.EvaluateExpression(expression, NormalizeContext(context));
        if (result.IsFailure)
            throw new InvalidOperationException($"Invalid FEEL expression '{expression}': {result.Error}");
        return NormalizeResult(result.GetValueOrThrow().ToObject());
    }

    public static bool EvaluateUnaryTests(
        string unaryTests,
        object? input,
        IReadOnlyDictionary<string, object> context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unaryTests);
        var normalizedInput = NormalizeInput(input);
        var normalizedContext = NormalizeContext(context);
        var contextual = Engine.EvaluateUnaryTests(unaryTests, normalizedInput!, normalizedContext);
        if (contextual.IsSuccess && contextual.GetValueOrThrow() == true) return true;

        // The package has separate execution paths for context-free and contextual
        // unary tests. Keep the context-free path authoritative when no referenced
        // context value is needed; this is required for standard range tests.
        var standalone = Engine.EvaluateUnaryTests(unaryTests, normalizedInput!);
        if (standalone.IsSuccess && standalone.GetValueOrThrow() == true) return true;

        // Preserve the pre-FEEL API's numeric-string compatibility without changing
        // strict FEEL evaluation for typed values.
        if (normalizedInput is string text
            && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            var numericContextual = Engine.EvaluateUnaryTests(unaryTests, number, normalizedContext);
            if (numericContextual.IsSuccess && numericContextual.GetValueOrThrow() == true) return true;
            var numericStandalone = Engine.EvaluateUnaryTests(unaryTests, number);
            if (numericStandalone.IsSuccess && numericStandalone.GetValueOrThrow() == true) return true;
        }

        if (contextual.IsFailure && standalone.IsFailure)
            throw new InvalidOperationException(
                $"Invalid FEEL unary test '{unaryTests}': {contextual.Error ?? standalone.Error}");
        return false;
    }

    public static void ValidateExpression(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var result = Engine.ParseExpression(expression);
        if (result.IsFailure)
            throw new InvalidOperationException($"Invalid FEEL expression '{expression}': {result.Error}");
    }

    private static Dictionary<string, object> NormalizeContext(IReadOnlyDictionary<string, object> context) =>
        context.ToDictionary(
            entry => entry.Key,
            entry => NormalizeInput(entry.Value)!,
            StringComparer.Ordinal);

    private static object? NormalizeInput(object? value)
    {
        if (value is not JsonElement json) return value;
        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number when json.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Array => json.EnumerateArray().Select(item => NormalizeInput(item)).ToArray(),
            JsonValueKind.Object => json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => NormalizeInput(property.Value)!,
                StringComparer.Ordinal),
            _ => json.GetRawText()
        };
    }

    private static object? NormalizeResult(object? value) => value switch
    {
        null => null,
        IDictionary dictionary => dictionary.Keys.Cast<object>()
            .ToDictionary(
                key => key.ToString() ?? string.Empty,
                key => NormalizeResult(dictionary[key])!,
                StringComparer.Ordinal),
        IEnumerable sequence when value is not string => sequence.Cast<object?>()
            .Select(NormalizeResult)
            .ToList(),
        _ => value
    };
}
