namespace VertexBPMN.Api.Features;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type AdvancedFeatureOptions retained for exposed HTTP action signatures and controller activation (AdvancedFeatureOptions).")]
public sealed class AdvancedFeatureOptions
{
    public const string SectionName = "AdvancedFeatures";

    public bool SimulationExecution { get; set; }
    public bool LiveProcessMigration { get; set; }
    public bool CmmnExecution { get; set; }
}
