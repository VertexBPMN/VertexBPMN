using System.Collections.Frozen;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// The CLOSED action catalog (LC07 S3). The designer only ever offers these action types at
/// version 1.0.0, matching the <c>ApplicationComponentCatalog.Actions</c> allowlist subset the
/// S3 slice covers (Button-Ausführung). An unknown/unsanctioned type is rejected, never emitted.
/// Each type declares what its reference-based <see cref="DesignerAction.Target"/> means and
/// which JSON property it is serialized into (matching the Lc06ReferenceApp action shape).
/// </summary>
internal static class ApplicationActionCatalog
{
    public const string Version = "1.0.0";

    /// <summary>The closed S3 action catalog (ordered for a stable palette).</summary>
    public static IReadOnlySet<string> Types { get; } = new[]
    {
        "data-source-read", "start-process", "claim-task", "complete-task"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The ContentJson property an action's target is written to, by action type.
    /// data-source-read -> dataSource (declared source key); start-process ->
    /// processDefinitionId (stable process KEY, per the released runtime which starts by key);
    /// claim/complete-task -> taskKey (the task the button executes against).
    /// </summary>
    public static string TargetPropertyFor(string type) => type switch
    {
        "data-source-read" => "dataSource",
        "start-process" => "processDefinitionId",
        "claim-task" => "taskKey",
        "complete-task" => "taskKey",
        _ => throw new InvalidOperationException($"Unsupported action type '{type}'.")
    };

    public static bool IsKnown(string type) => Types.Contains(type);

    /// <summary>Whether an action type resolves its target against a closed reference catalogue (validated at authoring time).</summary>
    public static bool RequiresValidatedReference(string type) => type is "data-source-read" or "start-process";
}
