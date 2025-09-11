namespace VertexBPMN.Core.Features;

/// <summary>
/// Simple static feature flag registry for experimental/advanced features.
/// In real-world use, replace with config/db-driven or LaunchDarkly/Unleash integration.
/// </summary>
public static class FeatureFlags
{
    public static bool LiveInspector { get; set; } = true;
    public static bool PredictiveAnalytics { get; set; } = false;
    public static bool ProcessMiningApi { get; set; } = false;
    // Add more flags as needed
}
