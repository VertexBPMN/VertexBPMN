namespace VertexBPMN.Application;

/// <summary>
/// Public entry point for standards-based FEEL expression evaluation outside
/// the DMN application services. The embedded runtime and its isolation remain
/// implemented by <see cref="FeelEvaluator"/>.
/// </summary>
public static class FeelExpressionRuntime
{
    public static object? Evaluate(
        string expression,
        IReadOnlyDictionary<string, object> context) =>
        FeelEvaluator.EvaluateExpression(expression, context);
}
