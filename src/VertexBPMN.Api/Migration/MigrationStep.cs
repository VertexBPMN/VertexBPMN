namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type MigrationStep retained for exposed HTTP action signatures and controller activation (MigrationStep).")]
public class MigrationStep
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Order { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new(StringComparer.Ordinal);
}
