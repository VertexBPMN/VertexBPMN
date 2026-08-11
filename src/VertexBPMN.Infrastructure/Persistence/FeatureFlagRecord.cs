namespace VertexBPMN.Infrastructure.Persistence;

public sealed class FeatureFlagRecord
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}