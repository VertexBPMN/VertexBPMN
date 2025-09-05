namespace VertexBPMN.Api.Migration;

public class MigrationStep
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Order { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}