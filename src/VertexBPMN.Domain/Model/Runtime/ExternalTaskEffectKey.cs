namespace VertexBPMN.Domain.Model.Runtime;

/// <summary>
/// Canonical stable business-effect key shared by the worker adapter template and the
/// server-persisted effect receipt. Bound to the activity execution (lease-generation
/// independent) so a reclaim or retry maps to the same key and cannot double-register.
/// </summary>
public static class ExternalTaskEffectKey
{
    public const string DefaultEffectName = "primary";

    public static string For(Guid activityExecutionId, string effectName = DefaultEffectName)
        => $"external-task:{activityExecutionId:N}:{effectName}";
}
