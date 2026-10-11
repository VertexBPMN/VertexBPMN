namespace VertexBPMN.Studio.Services;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract used by framework-discovered MVC controllers or Razor component parameters.")]
public sealed record StudioExternalTaskProfile(
    string ProfileRef, string ProfileVersion, string Topic, int MaxAttempts, int MaxDeadlineSeconds,
    IReadOnlyList<StudioExternalTaskField> Inputs, IReadOnlyList<StudioExternalTaskField> Outputs);
