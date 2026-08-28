using System.Globalization;
using System.Text.Json;
using Jint;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Application;

/// <summary>
/// Executes FEEL expressions and unary tests through the pinned, embedded
/// feelin runtime. A thread-local Jint realm keeps requests isolated while
/// avoiding bundle parsing for every decision evaluation.
/// </summary>
internal static class FeelEvaluator
{
    private const string ResourceName = "VertexBPMN.Application.FeelRuntime.feelin.bundle.js";
    private static readonly Lazy<string> RuntimeSource = new(LoadRuntimeSource);
    private static readonly ThreadLocal<Jint.Engine> Engines = new(CreateEngine);

    public static object? EvaluateExpression(
        string expression,
        IReadOnlyDictionary<string, object> context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var result = Invoke(
            "vertexFeelEvaluate",
            expression,
            SerializeContext(context));
        return ReadValue(result);
    }

    public static bool EvaluateUnaryTests(
        string unaryTests,
        object? input,
        IReadOnlyDictionary<string, object> context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unaryTests);
        var normalizedInput = NormalizeInput(input);
        var result = Invoke(
            "vertexFeelUnaryTest",
            unaryTests,
            JsonSerializer.Serialize(normalizedInput),
            SerializeContext(context));
        var value = ReadValue(result);
        if (value is true) return true;

        // Preserve the established API's numeric-string compatibility for
        // already deployed tables while the strict FEEL path remains primary.
        if (normalizedInput is string text
            && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            var numericResult = Invoke(
                "vertexFeelUnaryTest",
                unaryTests,
                JsonSerializer.Serialize(number),
                SerializeContext(context));
            if (ReadValue(numericResult) is true) return true;
        }

        return DmnDecisionTable.MatchesLegacyUnaryTest(unaryTests, normalizedInput);
    }

    public static void ValidateExpression(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        Invoke("vertexFeelValidateExpression", expression);
    }

    public static void ValidateUnaryTests(string unaryTests)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unaryTests);
        Invoke("vertexFeelValidateUnaryTests", unaryTests);
    }

    private static string Invoke(string functionName, params object?[] arguments)
    {
        try
        {
            return Engines.Value!.Invoke(functionName, arguments).AsString();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"FEEL runtime rejected input in '{functionName}': {exception.Message}",
                exception);
        }
    }

    private static Jint.Engine CreateEngine()
    {
        var engine = new Jint.Engine(options => options
            .TimeoutInterval(TimeSpan.FromSeconds(2))
            .LimitRecursion(256)
            .MaxStatements(2_000_000));
        engine.Execute(RuntimeSource.Value);
        return engine;
    }

    private static string LoadRuntimeSource()
    {
        using var stream = typeof(FeelEvaluator).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded FEEL runtime resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string SerializeContext(IReadOnlyDictionary<string, object> context) =>
        JsonSerializer.Serialize(context.ToDictionary(
            entry => entry.Key,
            entry => NormalizeInput(entry.Value),
            StringComparer.Ordinal));

    private static object? ReadValue(string resultJson)
    {
        using var document = JsonDocument.Parse(resultJson);
        if (document.RootElement.TryGetProperty("warnings", out var warnings)
            && warnings.ValueKind == JsonValueKind.Array
            && warnings.GetArrayLength() > 0)
        {
            var messages = warnings.EnumerateArray()
                .Select(warning => warning.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : warning.GetRawText())
                .Where(message => !string.IsNullOrWhiteSpace(message));
            throw new InvalidOperationException($"FEEL evaluation failed: {string.Join("; ", messages)}");
        }
        if (!document.RootElement.TryGetProperty("value", out var value))
            throw new InvalidOperationException("FEEL runtime returned no value.");
        return NormalizeResult(value);
    }

    private static object? NormalizeInput(object? value)
    {
        if (value is not JsonElement json) return value;
        return NormalizeResult(json);
    }

    private static object? NormalizeResult(JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.String => json.GetString(),
        JsonValueKind.Number when json.TryGetDecimal(out var number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Array => json.EnumerateArray().Select(NormalizeResult).ToList(),
        JsonValueKind.Object => json.EnumerateObject().ToDictionary(
            property => property.Name,
            property => NormalizeResult(property.Value)!,
            StringComparer.Ordinal),
        _ => json.GetRawText()
    };
}
