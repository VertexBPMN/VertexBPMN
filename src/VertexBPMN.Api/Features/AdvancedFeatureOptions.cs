namespace VertexBPMN.Api.Features;

public sealed class AdvancedFeatureOptions
{
    public const string SectionName = "AdvancedFeatures";

    public bool SimulationExecution { get; set; }
    public bool LiveProcessMigration { get; set; }
    public bool CmmnExecution { get; set; }
}
